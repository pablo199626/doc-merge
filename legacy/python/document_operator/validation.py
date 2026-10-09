"""Post-generation integrity checks for transferred DOCX files."""

import hashlib
import zipfile
from copy import deepcopy
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Tuple
from xml.etree import ElementTree as ET

from document_operator.docx_processor import (
    CONTENT_TYPES_PART,
    DOCUMENT_PART,
    DOCUMENT_RELATIONSHIPS_PART,
    NAMESPACES,
    NUMBERING_PART,
    RELATIONSHIP_ATTRIBUTES,
    STYLES_PART,
    STYLE_TAGS,
    WORD_NAMESPACE,
    W_VAL,
    DocxReadResult,
    DocxRecord,
    _locate_strict_table,
    read_docx_records,
)
from document_operator.matching import TransferPlan
from document_operator.transfer_types import ActionType


ALLOWED_CHANGED_PARTS = frozenset({
    DOCUMENT_PART,
    STYLES_PART,
    NUMBERING_PART,
    DOCUMENT_RELATIONSHIPS_PART,
    CONTENT_TYPES_PART,
})


class TransferValidationError(ValueError):
    """The generated document does not faithfully implement its plan."""


@dataclass(frozen=True)
class TransferValidationRequest:
    result_path: Path
    destination_path: Path
    plan: TransferPlan
    sort_key: Callable[[str], Tuple[str, str, str]]
    header: str


def _normalize_element_references(element: ET.Element) -> None:
    is_reference = (
        element.tag in STYLE_TAGS
        or element.tag == f"{{{WORD_NAMESPACE}}}numId"
    )
    if is_reference and element.get(W_VAL) is not None:
        element.set(W_VAL, "REFERENCIA_REMAPEADA")
    for attribute in tuple(element.attrib):
        if attribute in RELATIONSHIP_ATTRIBUTES:
            element.set(attribute, "REFERENCIA_REMAPEADA")


def _create_row_signature(row: ET.Element) -> bytes:
    clone = deepcopy(row)
    for element in clone.iter():
        _normalize_element_references(element)
    return ET.tostring(clone)


def _remove_people_table(xml_content: bytes, header: str) -> bytes:
    root = ET.fromstring(xml_content)
    _, table, _, _ = _locate_strict_table(root, header)
    parent = next((element for element in root.iter() if table in list(element)), None)
    if parent is None:
        raise TransferValidationError(
            "No se pudo aislar la tabla objetivo durante la validación."
        )
    parent.remove(table)
    return ET.tostring(root)


def _read_declared_ids(package: Dict[str, bytes], part: str, query: str, attribute: str) -> set:
    if part not in package:
        return set()
    root = ET.fromstring(package[part])
    return {
        element.get(attribute)
        for element in root.findall(query, NAMESPACES)
        if element.get(attribute) is not None
    }


def _validate_record_references(package: Dict[str, bytes], result: DocxReadResult) -> None:
    styles = _read_declared_ids(
        package,
        STYLES_PART,
        "./w:style",
        f"{{{WORD_NAMESPACE}}}styleId",
    )
    numbering = _read_declared_ids(
        package,
        NUMBERING_PART,
        "./w:num",
        f"{{{WORD_NAMESPACE}}}numId",
    )
    for record in result.records:
        missing_styles = set(record.dependencies.styles) - styles
        if missing_styles:
            raise TransferValidationError(
                f"La fila {record.location} usa estilos inexistentes: "
                f"{sorted(missing_styles)}"
            )
        missing_numbering = set(record.dependencies.numbering) - numbering - {"0"}
        if missing_numbering:
            raise TransferValidationError(
                f"La fila {record.location} usa numeraciones inexistentes: "
                f"{sorted(missing_numbering)}"
            )


def _expected_records(plan: TransferPlan) -> List[DocxRecord]:
    records = []
    for action in plan.actions:
        if action.action_type in {ActionType.ADD, ActionType.REPLACE}:
            records.append(action.source)
            continue
        if action.action_type is ActionType.KEEP and action.destination is not None:
            records.append(action.destination)
    return records


def _validate_records(
    result: DocxReadResult,
    expected_records: List[DocxRecord],
    sort_key: Callable[[str], Tuple[str, str, str]],
) -> None:
    ordered_records = sorted(
        expected_records, key=lambda record: sort_key(record.original_name)
    )
    if len(result.records) != len(ordered_records):
        raise TransferValidationError(
            f"El resultado contiene {len(result.records)} personas; "
            f"se esperaban {len(ordered_records)}."
        )
    for actual, expected in zip(result.records, ordered_records):
        if actual.identity_key != expected.identity_key:
            raise TransferValidationError(
                f"Orden o identidad incorrectos: {actual.original_name} no corresponde "
                f"a {expected.original_name}."
            )
        if _create_row_signature(actual.row_xml) != _create_row_signature(expected.row_xml):
            raise TransferValidationError(
                f"La información o formato de {actual.original_name} no coincide "
                "con su procedencia."
            )


def _validate_destination_parts(
    destination_archive: zipfile.ZipFile,
    result_package: Dict[str, bytes],
) -> None:
    for part_name in destination_archive.namelist():
        if part_name not in result_package:
            raise TransferValidationError(
                f"El resultado perdió una parte del destino: {part_name}."
            )
        if part_name in ALLOWED_CHANGED_PARTS:
            continue
        if destination_archive.read(part_name) != result_package[part_name]:
            raise TransferValidationError(
                f"Se modificó una parte no autorizada del destino: {part_name}."
            )


def _validate_package(request: TransferValidationRequest, result: DocxReadResult) -> None:
    with zipfile.ZipFile(request.result_path) as result_archive, zipfile.ZipFile(
        request.destination_path
    ) as destination_archive:
        corrupt_part = result_archive.testzip()
        if corrupt_part:
            raise TransferValidationError(
                f"El paquete generado contiene una parte corrupta: {corrupt_part}."
            )
        package = {
            name: result_archive.read(name) for name in result_archive.namelist()
        }
        _validate_destination_parts(destination_archive, package)
        destination_body = _remove_people_table(
            destination_archive.read(DOCUMENT_PART), request.header
        )
        result_body = _remove_people_table(
            package[DOCUMENT_PART], request.header
        )
        if destination_body != result_body:
            raise TransferValidationError(
                "Se modificó contenido del destino situado fuera de la tabla objetivo."
            )
        _validate_record_references(package, result)


def validate_transfer_result(request: TransferValidationRequest) -> DocxReadResult:
    """Validate records, provenance, ordering and package integrity."""
    result = read_docx_records(request.result_path, request.header)
    _validate_records(result, _expected_records(request.plan), request.sort_key)
    _validate_package(request, result)
    return result


def verify_file_hash(path: Path, expected_hash: str, description: str) -> None:
    if not expected_hash:
        return
    actual_hash = hashlib.sha256(Path(path).read_bytes()).hexdigest()
    if actual_hash != expected_hash:
        raise TransferValidationError(
            f"El archivo {description} cambió durante la operación."
        )
    STYLES_PART,
