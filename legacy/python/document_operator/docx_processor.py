"""Low-level DOCX processing that preserves every document element.

A DOCX is a ZIP package. This module copies every package entry unchanged and
only reorders complete ``w:tr`` XML nodes in ``word/document.xml``.
"""

import shutil
import tempfile
import zipfile
import hashlib
import io
import posixpath
import re
from copy import deepcopy
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Set, Tuple
from xml.etree import ElementTree as ET

from document_operator.identity import normalize_name
from document_operator.transfer_types import ActionType


WORD_NAMESPACE = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NAMESPACES = {"w": WORD_NAMESPACE}
W_TR = f"{{{WORD_NAMESPACE}}}tr"
RELATIONSHIP_NAMESPACE = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PACKAGE_RELATIONSHIP_NAMESPACE = "http://schemas.openxmlformats.org/package/2006/relationships"
CONTENT_TYPES_NAMESPACE = "http://schemas.openxmlformats.org/package/2006/content-types"
W_VAL = f"{{{WORD_NAMESPACE}}}val"
DOCUMENT_PART = "word/document.xml"
DOCUMENT_RELATIONSHIPS_PART = "word/_rels/document.xml.rels"
STYLES_PART = "word/styles.xml"
NUMBERING_PART = "word/numbering.xml"
CONTENT_TYPES_PART = "[Content_Types].xml"
DOCX_EXTENSION = ".docx"
TEMPORARY_PREFIX = "document_operator_"
SUPPORTED_COLUMN_COUNT = 3
NO_NUMBERING_ID = "0"
RELATIONSHIP_ATTRIBUTES = {
    f"{{{RELATIONSHIP_NAMESPACE}}}id",
    f"{{{RELATIONSHIP_NAMESPACE}}}embed",
    f"{{{RELATIONSHIP_NAMESPACE}}}link",
}
STYLE_TAGS = {
    f"{{{WORD_NAMESPACE}}}pStyle",
    f"{{{WORD_NAMESPACE}}}rStyle",
    f"{{{WORD_NAMESPACE}}}tblStyle",
}
UNSUPPORTED_TAGS = {
    f"{{{WORD_NAMESPACE}}}vMerge": "celdas combinadas verticalmente",
    f"{{{WORD_NAMESPACE}}}gridSpan": "celdas combinadas horizontalmente",
    f"{{{WORD_NAMESPACE}}}tbl": "tablas anidadas",
    f"{{{WORD_NAMESPACE}}}ins": "control de cambios",
    f"{{{WORD_NAMESPACE}}}del": "control de cambios",
    f"{{{WORD_NAMESPACE}}}moveFrom": "control de cambios",
    f"{{{WORD_NAMESPACE}}}moveTo": "control de cambios",
    f"{{{WORD_NAMESPACE}}}commentReference": "comentarios",
    f"{{{WORD_NAMESPACE}}}footnoteReference": "notas al pie",
    f"{{{WORD_NAMESPACE}}}endnoteReference": "notas finales",
}


def _register_namespaces(xml_content):
    for _, declaration in ET.iterparse(io.BytesIO(xml_content), events=("start-ns",)):
        prefix, uri = declaration
        try:
            ET.register_namespace(prefix or "", uri)
        except ValueError:
            pass


def _serialize_xml(root, original_xml=None):
    """Serialize while retaining declarations referenced only by mc:Ignorable."""
    if original_xml:
        _register_namespaces(original_xml)
    serialized = ET.tostring(root, encoding="utf-8", xml_declaration=True)
    if not original_xml:
        return serialized
    original_start = original_xml.find(b"<", original_xml.find(b"?>") + 2)
    original_end = original_xml.find(b">", original_start)
    original_header = original_xml[original_start:original_end]
    declarations = re.findall(rb'\s+xmlns(?::[A-Za-z_][\w.-]*)?="[^"]+"', original_header)
    root_start = serialized.find(b"<", serialized.find(b"?>") + 2)
    root_end = serialized.find(b">", root_start)
    new_header = serialized[root_start:root_end]
    missing_declarations = []
    for declaration in declarations:
        attribute = declaration.strip().split(b"=", 1)[0]
        if attribute + b"=" not in new_header:
            missing_declarations.append(declaration)
    if missing_declarations:
        serialized = serialized[:root_end] + b"".join(missing_declarations) + serialized[root_end:]
    return serialized


class DocxStructureError(ValueError):
    """The DOCX cannot be interpreted safely using the supported profile."""


@dataclass(frozen=True)
class RelationshipReference:
    relationship_id: str
    relationship_type: str
    target: str
    is_external: bool


@dataclass(frozen=True)
class RecordDependencies:
    styles: Tuple[str, ...]
    numbering: Tuple[str, ...]
    relationships: Tuple[RelationshipReference, ...]


@dataclass(frozen=True)
class DocxRecord:
    document_path: Path
    table_index: int
    row_index: int
    original_name: str
    identity_key: str
    row_xml: ET.Element
    dependencies: RecordDependencies

    @property
    def location(self):
        return f"tabla {self.table_index}, fila {self.row_index}"


@dataclass(frozen=True)
class AuxiliaryRow:
    row_index: int
    row_xml: ET.Element


@dataclass(frozen=True)
class DocxReadResult:
    document_path: Path
    sha256: str
    table_index: int
    header_index: int
    name_column_index: int
    records: Tuple[DocxRecord, ...]
    auxiliary_rows: Tuple[AuxiliaryRow, ...]

    @property
    def duplicates(self):
        grouped_records: Dict[str, List[DocxRecord]] = {}
        for record in self.records:
            grouped_records.setdefault(record.identity_key, []).append(record)
        return {
            identity_key: tuple(records)
            for identity_key, records in grouped_records.items()
            if len(records) > 1
        }


@dataclass(frozen=True)
class StyleImportResult:
    mapping: Dict[str, str]
    root: Optional[ET.Element]
    imported_ids: Set[str]


@dataclass(frozen=True)
class NumberingImportRequest:
    source_package: Dict[str, bytes]
    destination_package: Dict[str, bytes]
    source_rows: Sequence[ET.Element]
    styles: StyleImportResult


@dataclass(frozen=True)
class NumberingImportResult:
    mapping: Dict[str, str]
    root: Optional[ET.Element]


@dataclass(frozen=True)
class DocxTransferRequest:
    source_path: Path
    destination_path: Path
    output_path: Path
    plan: Any
    header: str = "Nombre y Apellidos"


@dataclass(frozen=True)
class PackageTransferData:
    source: Dict[str, bytes]
    destination: Dict[str, bytes]
    destination_infos: Sequence[zipfile.ZipInfo]


@dataclass(frozen=True)
class DependencyMappings:
    styles: Dict[str, str]
    numbering: Dict[str, str]
    relationships: Dict[str, str]


@dataclass(frozen=True)
class RecordReadContext:
    document_path: Path
    table_index: int
    name_column_index: int
    relationships: Dict[str, ET.Element]
    package_parts: Set[str]


@dataclass(frozen=True)
class StyleImportContext:
    dependency_ids: Sequence[str]
    source_styles: Dict[str, ET.Element]
    destination_styles: Dict[str, ET.Element]
    destination_root: ET.Element
    mapping: Dict[str, str]


def get_element_text(element):
    """Return visible text from all text runs inside a Word XML element."""
    return "".join(node.text or "" for node in element.findall(".//w:t", NAMESPACES))


def get_row_cells(row):
    """Return the direct table cells of a row."""
    return row.findall("./w:tc", NAMESPACES)


def _find_header_cells(row, header_key):
    return [
        cell_index
        for cell_index, cell in enumerate(get_row_cells(row))
        if normalize_name(get_element_text(cell)) == header_key
    ]


def _find_table_header_matches(table_index, table, header_key):
    matches = []
    for row_index, row in enumerate(table.findall("./w:tr", NAMESPACES)):
        matches.extend(
            (table_index, table, row_index, cell_index)
            for cell_index in _find_header_cells(row, header_key)
        )
    return matches


def _locate_strict_table(root, header):
    header_key = normalize_name(header)
    matches = [
        match
        for table_index, table in enumerate(root.findall(".//w:tbl", NAMESPACES))
        for match in _find_table_header_matches(table_index, table, header_key)
    ]
    if not matches:
        raise DocxStructureError(f'No se encontró una tabla con la cabecera exacta "{header}".')
    if len(matches) > 1:
        locations = ", ".join(
            f"tabla {table_index}, fila {row_index}"
            for table_index, _, row_index, _ in matches
        )
        raise DocxStructureError(f"La tabla de personas es ambigua: {locations}.")
    return matches[0]


def _read_document_relationships(archive):
    path = DOCUMENT_RELATIONSHIPS_PART
    if path not in archive.namelist():
        return {}
    root = ET.fromstring(archive.read(path))
    relationships = {}
    tag = f"{{{PACKAGE_RELATIONSHIP_NAMESPACE}}}Relationship"
    for relationship in root.findall(tag):
        relationship_id = relationship.get("Id")
        if relationship_id:
            relationships[relationship_id] = relationship
    return relationships


def _normalize_relationship_target(target):
    normalized_target = posixpath.normpath(posixpath.join("word", target))
    if target.startswith("/"):
        normalized_target = posixpath.normpath(target.lstrip("/"))
    if normalized_target == ".." or normalized_target.startswith("../"):
        raise DocxStructureError(f"Una relación apunta fuera del paquete DOCX: {target}")
    return normalized_target


def _collect_attribute_values(row, tags, attribute):
    return sorted({
        element.get(attribute)
        for element in row.iter()
        if element.tag in tags and element.get(attribute)
    })


def _collect_numbering_ids(row):
    return sorted({
        element.get(W_VAL)
        for element in row.iter(f"{{{WORD_NAMESPACE}}}numId")
        if element.get(W_VAL)
    })


def _collect_relationship_ids(row):
    return sorted({
        value
        for element in row.iter()
        for attribute, value in element.attrib.items()
        if attribute in RELATIONSHIP_ATTRIBUTES
    })


def _resolve_relationship_reference(relationship_id, relationships, package_parts):
    if relationship_id not in relationships:
        raise DocxStructureError(
            f"La fila contiene una relación no resuelta: {relationship_id}."
        )
    relationship = relationships[relationship_id]
    is_external = relationship.get("TargetMode", "").casefold() == "external"
    original_target = relationship.get("Target", "")
    target = original_target
    if not is_external:
        target = _normalize_relationship_target(original_target)
    if not is_external and target not in package_parts:
        raise DocxStructureError(
            f"La relación {relationship_id} apunta a una parte inexistente: {target}."
        )
    return RelationshipReference(
        relationship_id,
        relationship.get("Type", ""),
        target,
        is_external,
    )


def _find_row_dependencies(row, relationships, package_parts):
    styles = _collect_attribute_values(row, STYLE_TAGS, W_VAL)
    numbering = _collect_numbering_ids(row)
    references = tuple(
        _resolve_relationship_reference(relationship_id, relationships, package_parts)
        for relationship_id in _collect_relationship_ids(row)
    )
    return RecordDependencies(tuple(styles), tuple(numbering), references)


def _validate_table_structure(table):
    if table.findall(".//w:tbl", NAMESPACES):
        raise DocxStructureError("La tabla de personas contiene tablas anidadas no admitidas.")
    for tag, description in UNSUPPORTED_TAGS.items():
        if tag == f"{{{WORD_NAMESPACE}}}tbl":
            continue
        if any(True for _ in table.iter(tag)):
            raise DocxStructureError(f"La tabla de personas contiene {description}, que aún no se admite.")


def _validate_docx_path(docx_path):
    path = Path(docx_path)
    if path.suffix.casefold() != DOCX_EXTENSION:
        raise DocxStructureError("El documento debe tener extensión .docx.")
    if not path.is_file():
        raise DocxStructureError(f"No existe el documento DOCX: {path}")
    return path


def _validate_row_cells(row_index, row):
    cells = get_row_cells(row)
    if len(cells) != SUPPORTED_COLUMN_COUNT:
        raise DocxStructureError(
            f"La fila {row_index} tiene {len(cells)} celdas; "
            "el perfil admite exactamente 3."
        )
    return cells


def _create_record(context, row_index, name, row):
    return DocxRecord(
        context.document_path,
        context.table_index,
        row_index,
        name,
        normalize_name(name),
        deepcopy(row),
        _find_row_dependencies(
            row, context.relationships, context.package_parts
        ),
    )


def _read_table_rows(context, table, header_index):
    rows = table.findall("./w:tr", NAMESPACES)
    header_cells = get_row_cells(rows[header_index])
    if len(header_cells) != SUPPORTED_COLUMN_COUNT:
        raise DocxStructureError(
            f"La cabecera tiene {len(header_cells)} celdas; "
            "el perfil admite exactamente 3."
        )
    records = []
    auxiliary_rows = []
    indexed_rows = enumerate(rows[header_index + 1:], start=header_index + 1)
    for row_index, row in indexed_rows:
        cells = _validate_row_cells(row_index, row)
        cell_texts = [" ".join(get_element_text(cell).split()) for cell in cells]
        if not any(cell_texts):
            auxiliary_rows.append(AuxiliaryRow(row_index, deepcopy(row)))
            continue
        name = cell_texts[context.name_column_index]
        if not name:
            raise DocxStructureError(
                f"La fila {row_index} contiene datos, pero no tiene identidad "
                "en la columna de nombres."
            )
        records.append(_create_record(context, row_index, name, row))
    return records, auxiliary_rows


def _parse_docx_records(path, content, header):
    with zipfile.ZipFile(io.BytesIO(content), "r") as archive:
        package_parts = set(archive.namelist())
        if DOCUMENT_PART not in package_parts:
            raise DocxStructureError("El archivo no contiene word/document.xml.")
        root = ET.fromstring(archive.read(DOCUMENT_PART))
        relationships = _read_document_relationships(archive)
        location = _locate_strict_table(root, header)
        table_index, table, header_index, name_column_index = location
        _validate_table_structure(table)
        context = RecordReadContext(
            path, table_index, name_column_index, relationships, package_parts
        )
        records, auxiliary_rows = _read_table_rows(context, table, header_index)
    return table_index, header_index, name_column_index, records, auxiliary_rows


def read_docx_records(docx_path, header="Nombre y Apellidos"):
    """Read complete person rows and their direct package dependencies."""
    path = _validate_docx_path(docx_path)

    content = path.read_bytes()
    file_hash = hashlib.sha256(content).hexdigest()
    try:
        parsed = _parse_docx_records(path, content, header)
    except DocxStructureError:
        raise
    except (OSError, zipfile.BadZipFile, ET.ParseError, KeyError) as error:
        raise DocxStructureError(f"No se pudo leer el DOCX: {error}") from error
    table_index, header_index, name_column_index, records, auxiliary_rows = parsed
    if not records:
        raise DocxStructureError("La tabla encontrada no contiene registros de personas.")
    return DocxReadResult(
        document_path=path,
        sha256=file_hash,
        table_index=table_index,
        header_index=header_index,
        name_column_index=name_column_index,
        records=tuple(records),
        auxiliary_rows=tuple(auxiliary_rows),
    )


def _map_children_by_attribute(root, tag, attribute):
    return {
        element.get(attribute): element
        for element in root.findall(f"./w:{tag}", NAMESPACES)
        if element.get(attribute) is not None
    }


def _create_unique_id(base_id, used_ids):
    candidate = f"DO_{base_id}"
    number = 2
    while candidate in used_ids:
        candidate = f"DO_{base_id}_{number}"
        number += 1
    used_ids.add(candidate)
    return candidate


def _collect_style_references(source_rows):
    return {
        element.get(W_VAL)
        for row in source_rows
        for element in row.iter()
        if element.tag in STYLE_TAGS and element.get(W_VAL)
    }


def _require_style_parts(source_package, destination_package):
    if STYLES_PART not in source_package or STYLES_PART not in destination_package:
        raise DocxStructureError("No se pueden conservar los estilos: falta word/styles.xml.")
    source_root = ET.fromstring(source_package[STYLES_PART])
    destination_root = ET.fromstring(destination_package[STYLES_PART])
    source_styles = _map_children_by_attribute(source_root, "style", f"{{{WORD_NAMESPACE}}}styleId")
    destination_styles = _map_children_by_attribute(destination_root, "style", f"{{{WORD_NAMESPACE}}}styleId")
    return destination_root, source_styles, destination_styles


def _collect_style_dependencies(references, source_styles):
    pending_ids = list(references)
    dependency_ids = []
    while pending_ids:
        style_id = pending_ids.pop(0)
        if style_id in dependency_ids:
            continue
        if style_id not in source_styles:
            raise DocxStructureError(
                f"El origen referencia un estilo inexistente: {style_id}."
            )
        dependency_ids.append(style_id)
        pending_ids.extend(_get_linked_style_ids(source_styles[style_id]))
    return dependency_ids


def _get_linked_style_ids(style):
    return [
        reference.get(W_VAL)
        for tag in ("basedOn", "link")
        for reference in [style.find(f"w:{tag}", NAMESPACES)]
        if reference is not None and reference.get(W_VAL)
    ]


def _style_inherits_conflict(style_id, source_styles, conflicts):
    return bool(set(_get_linked_style_ids(source_styles[style_id])) & conflicts)


def _find_style_conflicts(dependency_ids, source_styles, destination_styles):
    conflicts = {
        style_id for style_id in dependency_ids
        if style_id in destination_styles
        and ET.tostring(source_styles[style_id]) != ET.tostring(destination_styles[style_id])
    }
    while True:
        new_conflicts = {
            style_id for style_id in dependency_ids
            if style_id not in conflicts
            and style_id in destination_styles
            and _style_inherits_conflict(style_id, source_styles, conflicts)
        }
        if not new_conflicts:
            return conflicts
        conflicts.update(new_conflicts)


def _create_style_mapping(dependency_ids, conflicts, destination_styles):
    used_ids = set(destination_styles)
    mapping = {}
    for style_id in dependency_ids:
        if style_id not in conflicts:
            mapping[style_id] = style_id
            continue
        mapping[style_id] = _create_unique_id(style_id, used_ids)
    return mapping


def _clone_style(style_id, context):
    new_id = context.mapping[style_id]
    clone = deepcopy(context.source_styles[style_id])
    clone.set(f"{{{WORD_NAMESPACE}}}styleId", new_id)
    for tag in ("basedOn", "link"):
        reference = clone.find(f"w:{tag}", NAMESPACES)
        if reference is not None and reference.get(W_VAL) in context.mapping:
            reference.set(W_VAL, context.mapping[reference.get(W_VAL)])
    context.destination_root.append(clone)
    return new_id


def _import_styles(context):
    imported_ids = set()
    for style_id in context.dependency_ids:
        if context.mapping[style_id] in context.destination_styles:
            continue
        new_id = _clone_style(style_id, context)
        imported_ids.add(new_id)
    return imported_ids


def _prepare_styles(source_package, destination_package, source_rows):
    references = _collect_style_references(source_rows)
    if not references:
        return StyleImportResult({}, None, set())
    destination_root, source_styles, destination_styles = _require_style_parts(
        source_package, destination_package
    )
    dependency_ids = _collect_style_dependencies(references, source_styles)
    conflicts = _find_style_conflicts(
        dependency_ids, source_styles, destination_styles
    )
    mapping = _create_style_mapping(dependency_ids, conflicts, destination_styles)
    context = StyleImportContext(
        dependency_ids, source_styles, destination_styles, destination_root, mapping
    )
    imported_ids = _import_styles(context)
    return StyleImportResult(mapping, destination_root, imported_ids)


def _next_numeric_id(values):
    numbers = [int(value) for value in values if value is not None and value.isdigit()]
    return max(numbers, default=0) + 1


def _collect_numbering_references(request):
    references = {
        element.get(W_VAL)
        for row in request.source_rows
        for element in row.iter(f"{{{WORD_NAMESPACE}}}numId")
        if element.get(W_VAL) not in (None, NO_NUMBERING_ID)
    }
    if not request.styles.imported_ids or STYLES_PART not in request.source_package:
        return references
    source_styles_root = ET.fromstring(request.source_package[STYLES_PART])
    for style in source_styles_root.findall("./w:style", NAMESPACES):
        original_id = style.get(f"{{{WORD_NAMESPACE}}}styleId")
        mapped_id = request.styles.mapping.get(original_id)
        if mapped_id not in request.styles.imported_ids:
            continue
        references.update(
            element.get(W_VAL)
            for element in style.iter(f"{{{WORD_NAMESPACE}}}numId")
            if element.get(W_VAL) not in (None, NO_NUMBERING_ID)
        )
    return references


def _numbering_id_sort_key(value):
    if value.isdigit():
        return 0, int(value)
    return 1, value


class _NumberingImporter:
    def __init__(self, request):
        self.request = request
        self.mapping = {}
        self.abstract_mapping = {}
        self._load_definitions()

    def _load_definitions(self):
        source_part = self.request.source_package.get(NUMBERING_PART)
        destination_part = self.request.destination_package.get(NUMBERING_PART)
        if source_part is None or destination_part is None:
            raise DocxStructureError(
                "No se puede conservar la numeración: falta word/numbering.xml."
            )
        source_root = ET.fromstring(source_part)
        self.destination_root = ET.fromstring(destination_part)
        self.source_numbers = _map_children_by_attribute(
            source_root, "num", f"{{{WORD_NAMESPACE}}}numId"
        )
        self.source_abstracts = _map_children_by_attribute(
            source_root, "abstractNum", f"{{{WORD_NAMESPACE}}}abstractNumId"
        )
        destination_numbers = _map_children_by_attribute(
            self.destination_root, "num", f"{{{WORD_NAMESPACE}}}numId"
        )
        destination_abstracts = _map_children_by_attribute(
            self.destination_root, "abstractNum", f"{{{WORD_NAMESPACE}}}abstractNumId"
        )
        self.next_number_id = _next_numeric_id(destination_numbers)
        self.next_abstract_id = _next_numeric_id(destination_abstracts)

    def import_references(self, references):
        for number_id in sorted(references, key=_numbering_id_sort_key):
            self._import_number(number_id)
        self._remap_style_numbering()
        return NumberingImportResult(self.mapping, self.destination_root)

    def _import_number(self, number_id):
        if number_id not in self.source_numbers:
            raise DocxStructureError(
                f"El origen referencia una numeración inexistente: {number_id}."
            )
        source_number = self.source_numbers[number_id]
        abstract_id = self._get_abstract_id(number_id, source_number)
        self._import_abstract(abstract_id)
        new_number_id = str(self.next_number_id)
        self.next_number_id += 1
        self.mapping[number_id] = new_number_id
        number_clone = deepcopy(source_number)
        number_clone.set(f"{{{WORD_NAMESPACE}}}numId", new_number_id)
        number_clone.find("w:abstractNumId", NAMESPACES).set(
            W_VAL, self.abstract_mapping[abstract_id]
        )
        self.destination_root.append(number_clone)

    def _get_abstract_id(self, number_id, source_number):
        reference = source_number.find("w:abstractNumId", NAMESPACES)
        if reference is None or reference.get(W_VAL) not in self.source_abstracts:
            raise DocxStructureError(
                f"La numeración {number_id} no tiene una definición abstracta válida."
            )
        return reference.get(W_VAL)

    def _import_abstract(self, abstract_id):
        if abstract_id in self.abstract_mapping:
            return
        new_abstract_id = str(self.next_abstract_id)
        self.next_abstract_id += 1
        self.abstract_mapping[abstract_id] = new_abstract_id
        clone = deepcopy(self.source_abstracts[abstract_id])
        clone.set(f"{{{WORD_NAMESPACE}}}abstractNumId", new_abstract_id)
        self._remap_abstract_styles(clone)
        children = list(self.destination_root)
        insertion_index = next((
            index for index, child in enumerate(children)
            if child.tag == f"{{{WORD_NAMESPACE}}}num"
        ), len(children))
        self.destination_root.insert(insertion_index, clone)

    def _remap_abstract_styles(self, abstract_definition):
        for tag in ("styleLink", "numStyleLink"):
            for reference in abstract_definition.findall(f".//w:{tag}", NAMESPACES):
                self._remap_style_reference(reference)

    def _remap_style_reference(self, reference):
        style_id = reference.get(W_VAL)
        if style_id in self.request.styles.mapping:
            reference.set(W_VAL, self.request.styles.mapping[style_id])

    def _remap_style_numbering(self):
        if self.request.styles.root is None:
            return
        for style in self.request.styles.root.findall("./w:style", NAMESPACES):
            style_id = style.get(f"{{{WORD_NAMESPACE}}}styleId")
            if style_id not in self.request.styles.imported_ids:
                continue
            self._remap_numbering_in_style(style)

    def _remap_numbering_in_style(self, style):
        for element in style.iter(f"{{{WORD_NAMESPACE}}}numId"):
            number_id = element.get(W_VAL)
            if number_id in self.mapping:
                element.set(W_VAL, self.mapping[number_id])


def _prepare_numbering(request):
    references = _collect_numbering_references(request)
    if not references:
        return NumberingImportResult({}, None)
    return _NumberingImporter(request).import_references(references)


def _get_relationships_root(package):
    path = DOCUMENT_RELATIONSHIPS_PART
    if path in package:
        return ET.fromstring(package[path])
    return ET.Element(f"{{{PACKAGE_RELATIONSHIP_NAMESPACE}}}Relationships")


def _map_relationships_by_id(root):
    return {
        element.get("Id"): element
        for element in root.findall(f"{{{PACKAGE_RELATIONSHIP_NAMESPACE}}}Relationship")
        if element.get("Id")
    }


def _next_relationship_id(used_ids):
    number = 1
    while f"rId{number}" in used_ids:
        number += 1
    result = f"rId{number}"
    used_ids.add(result)
    return result


def _create_unique_part_name(path, used_ids):
    if path not in used_ids:
        used_ids.add(path)
        return path
    directory, name = posixpath.split(path)
    base_id, extension = posixpath.splitext(name)
    number = 1
    while True:
        candidate = posixpath.join(directory, f"document_operator_{number}_{base_id}{extension}")
        if candidate not in used_ids:
            used_ids.add(candidate)
            return candidate
        number += 1


def _get_part_relationships_path(path):
    directory, name = posixpath.split(path)
    return posixpath.join(directory, "_rels", f"{name}.rels")


class _PartImporter:
    def __init__(self, source, target):
        self.source = source
        self.target = target
        self.mapping = {}
        self.used_ids = set(target)

    def import_part(self, source_path):
        if source_path in self.mapping:
            return self.mapping[source_path]
        if source_path not in self.source:
            raise DocxStructureError(f"El origen no contiene la parte relacionada: {source_path}.")
        destination_path = _create_unique_part_name(source_path, self.used_ids)
        self.mapping[source_path] = destination_path
        content = self.source[source_path]
        relationships_path = _get_part_relationships_path(source_path)
        if relationships_path in self.source:
            self._import_part_relationships(
                source_path, destination_path, relationships_path
            )
        self.target[destination_path] = content
        self._copy_content_type(source_path, destination_path)
        return destination_path

    def _import_part_relationships(
        self, source_path, destination_path, relationships_path
    ):
        original_relationships = self.source[relationships_path]
        root = ET.fromstring(original_relationships)
        relationship_tag = f"{{{PACKAGE_RELATIONSHIP_NAMESPACE}}}Relationship"
        for relationship in root.findall(relationship_tag):
            if relationship.get("TargetMode", "").casefold() == "external":
                continue
            target_path = _normalize_relative_path(
                source_path, relationship.get("Target", "")
            )
            imported_path = self.import_part(target_path)
            relative_path = posixpath.relpath(
                imported_path, posixpath.dirname(destination_path)
            )
            relationship.set("Target", relative_path)
        new_path = _get_part_relationships_path(destination_path)
        self.target[new_path] = _serialize_xml(root, original_relationships)

    def _copy_content_type(self, source, target):
        if CONTENT_TYPES_PART not in self.source or CONTENT_TYPES_PART not in self.target:
            return
        namespace = CONTENT_TYPES_NAMESPACE
        source_root = ET.fromstring(self.source[CONTENT_TYPES_PART])
        destination_root = ET.fromstring(self.target[CONTENT_TYPES_PART])
        override = next((
            element for element in source_root.findall(f"{{{namespace}}}Override")
            if element.get("PartName") == f"/{source}"
        ), None)
        has_target_override = any(
            element.get("PartName") == f"/{target}"
            for element in destination_root.findall(f"{{{namespace}}}Override")
        )
        if override is not None and not has_target_override:
            clone = deepcopy(override)
            clone.set("PartName", f"/{target}")
            destination_root.append(clone)
        if override is None:
            self._copy_default_content_type(
                source, source_root, destination_root
            )
        self.target[CONTENT_TYPES_PART] = _serialize_xml(
            destination_root, self.target[CONTENT_TYPES_PART]
        )

    def _copy_default_content_type(self, source, source_root, destination_root):
        namespace = CONTENT_TYPES_NAMESPACE
        extension = posixpath.splitext(source)[1].lstrip(".")
        source_default = next((
            element for element in source_root.findall(f"{{{namespace}}}Default")
            if element.get("Extension", "").casefold() == extension.casefold()
        ), None)
        destination_extensions = {
            element.get("Extension", "").casefold()
            for element in destination_root.findall(f"{{{namespace}}}Default")
        }
        if source_default is None or extension.casefold() in destination_extensions:
            return
        destination_root.append(deepcopy(source_default))


def _normalize_relative_path(base_part, target):
    if target.startswith("/"):
        return posixpath.normpath(target.lstrip("/"))
    return posixpath.normpath(posixpath.join(posixpath.dirname(base_part), target))


def _prepare_relationships(source_package, destination_package, source_rows):
    source_root = _get_relationships_root(source_package)
    destination_root = _get_relationships_root(destination_package)
    source_relationships = _map_relationships_by_id(source_root)
    used_ids = set(_map_relationships_by_id(destination_root))
    part_importer = _PartImporter(source_package, destination_package)
    mapping = {}
    relationship_ids = sorted({
        value
        for row in source_rows
        for element in row.iter()
        for attribute, value in element.attrib.items()
        if attribute in RELATIONSHIP_ATTRIBUTES
    })
    for relationship_id in relationship_ids:
        if relationship_id not in source_relationships:
            raise DocxStructureError(f"El origen no define la relación {relationship_id}.")
        original_xml = source_relationships[relationship_id]
        clone = deepcopy(original_xml)
        new_id = _next_relationship_id(used_ids)
        clone.set("Id", new_id)
        if clone.get("TargetMode", "").casefold() != "external":
            source_path = _normalize_relative_path(DOCUMENT_PART, clone.get("Target", ""))
            destination_path = part_importer.import_part(source_path)
            clone.set("Target", posixpath.relpath(destination_path, "word"))
        destination_root.append(clone)
        mapping[relationship_id] = new_id
    return mapping, destination_root


def _remap_relationship_attributes(element, relationships):
    for attribute in tuple(element.attrib):
        relationship_id = element.get(attribute)
        if attribute in RELATIONSHIP_ATTRIBUTES and relationship_id in relationships:
            element.set(attribute, relationships[relationship_id])


def _remap_row(row, styles, numbering, relationships):
    clone = deepcopy(row)
    for element in clone.iter():
        if element.tag in STYLE_TAGS and element.get(W_VAL) in styles:
            element.set(W_VAL, styles[element.get(W_VAL)])
        if element.tag == f"{{{WORD_NAMESPACE}}}numId" and element.get(W_VAL) in numbering:
            element.set(W_VAL, numbering[element.get(W_VAL)])
        _remap_relationship_attributes(element, relationships)
    return clone


def _replace_row(table, old_row, new_row):
    children = list(table)
    position = children.index(old_row)
    table.remove(old_row)
    table.insert(position, new_row)


def _write_original_parts(output_archive, destination_package, destination_infos):
    for entry_info in destination_infos:
        output_archive.writestr(
            entry_info, destination_package[entry_info.filename]
        )


def _write_new_parts(output_archive, destination_package, original_names):
    for name, content in destination_package.items():
        if name not in original_names:
            output_archive.writestr(name, content)


def _write_package(destination_package, destination_infos, output_path):
    directory = Path(output_path).resolve().parent
    directory.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(
        mode="wb", suffix=".docx", prefix=TEMPORARY_PREFIX, dir=directory, delete=False
    ) as temporary_file:
        temporary_path = Path(temporary_file.name)
    try:
        original_names = {entry_info.filename for entry_info in destination_infos}
        with zipfile.ZipFile(temporary_path, "w") as output_archive:
            _write_original_parts(
                output_archive, destination_package, destination_infos
            )
            _write_new_parts(output_archive, destination_package, original_names)
        shutil.move(temporary_path, output_path)
    finally:
        temporary_path.unlink(missing_ok=True)


def _read_transfer_packages(request):
    source_bytes = Path(request.source_path).read_bytes()
    destination_bytes = Path(request.destination_path).read_bytes()
    if hashlib.sha256(source_bytes).hexdigest() != request.plan.source_hash:
        raise DocxStructureError("El documento de origen cambió después de crear el plan.")
    if hashlib.sha256(destination_bytes).hexdigest() != request.plan.destination_hash:
        raise DocxStructureError("El documento de destino cambió después de crear el plan.")
    with zipfile.ZipFile(io.BytesIO(source_bytes)) as source_archive, zipfile.ZipFile(
        io.BytesIO(destination_bytes)
    ) as destination_archive:
        source_package = {
            name: source_archive.read(name) for name in source_archive.namelist()
        }
        destination_package = {
            name: destination_archive.read(name)
            for name in destination_archive.namelist()
        }
        destination_infos = destination_archive.infolist()
    return PackageTransferData(source_package, destination_package, destination_infos)


def _get_source_rows(plan):
    return [
        action.source.row_xml for action in plan.actions
        if action.action_type in {ActionType.ADD, ActionType.REPLACE}
        and action.source is not None
    ]


def _import_dependencies(package_data, source_rows):
    styles = _prepare_styles(
        package_data.source, package_data.destination, source_rows
    )
    numbering = _prepare_numbering(NumberingImportRequest(
        package_data.source, package_data.destination, source_rows, styles
    ))
    if styles.root is not None:
        package_data.destination[STYLES_PART] = _serialize_xml(
            styles.root, package_data.destination[STYLES_PART]
        )
    if numbering.root is not None:
        package_data.destination[NUMBERING_PART] = _serialize_xml(
            numbering.root, package_data.destination[NUMBERING_PART]
        )
    relationship_mapping, relationships_root = _prepare_relationships(
        package_data.source, package_data.destination, source_rows
    )
    package_data.destination[DOCUMENT_RELATIONSHIPS_PART] = _serialize_xml(
        relationships_root,
        package_data.destination.get(DOCUMENT_RELATIONSHIPS_PART),
    )
    return DependencyMappings(styles.mapping, numbering.mapping, relationship_mapping)


def _remap_action_row(action, mappings):
    return _remap_row(
        action.source.row_xml,
        mappings.styles,
        mappings.numbering,
        mappings.relationships,
    )


def _apply_actions(table, rows_by_index, plan, mappings):
    new_rows = []
    for action in plan.actions:
        if action.action_type is ActionType.DELETE and action.destination is not None:
            table.remove(rows_by_index[action.destination.row_index])
            continue
        if action.action_type is ActionType.REPLACE and action.destination is not None:
            _replace_row(
                table,
                rows_by_index[action.destination.row_index],
                _remap_action_row(action, mappings),
            )
            continue
        if action.action_type is ActionType.ADD:
            new_rows.append(_remap_action_row(action, mappings))
    for row in new_rows:
        table.append(row)
    return len(new_rows)


def apply_transfer_plan(request):
    """Apply a transfer plan to a copy of destination, preserving source rows."""
    package_data = _read_transfer_packages(request)
    mappings = _import_dependencies(package_data, _get_source_rows(request.plan))
    document_xml = package_data.destination[DOCUMENT_PART]
    document_root = ET.fromstring(document_xml)
    _, table, _, _ = _locate_strict_table(document_root, request.header)
    rows = table.findall("./w:tr", NAMESPACES)
    rows_by_index = {index: row for index, row in enumerate(rows)}
    added_count = _apply_actions(table, rows_by_index, request.plan, mappings)
    package_data.destination[DOCUMENT_PART] = _serialize_xml(
        document_root, document_xml
    )
    _write_package(
        package_data.destination,
        package_data.destination_infos,
        request.output_path,
    )
    return added_count


def _find_header_row_index(table, header):
    for index, row in enumerate(table.findall("./w:tr", NAMESPACES)):
        has_header = any(
            header in get_element_text(cell) for cell in get_row_cells(row)
        )
        if has_header:
            return index
    return None


def find_people_table(root, header="Nombre y Apellidos"):
    """Locate the table and header row containing the configured header text."""
    for table in root.findall(".//w:tbl", NAMESPACES):
        header_index = _find_header_row_index(table, header)
        if header_index is not None:
            return table, header_index
    raise ValueError(f'No se encontró una tabla con la cabecera "{header}".')


def _collect_people_rows(rows, header_index):
    positions = []
    people_rows = []
    for index, row in enumerate(rows[header_index + 1:], start=header_index + 1):
        cells = get_row_cells(row)
        if len(cells) < 2:
            continue
        name = get_element_text(cells[1]).strip()
        if name:
            positions.append(index)
            people_rows.append((row, name))
    return positions, people_rows


def _replace_table_rows(table, rows, replacements):
    final_sequence = [
        replacements.get(index, row) for index, row in enumerate(rows)
    ]
    table_children = list(table)
    first_row_index = next(
        index for index, child in enumerate(table_children) if child.tag == W_TR
    )
    for row in rows:
        table.remove(row)
    for offset, row in enumerate(final_sequence):
        table.insert(first_row_index + offset, row)


def reorder_table_rows(table, header_index, sort_key):
    """Reorder non-empty person rows after a header, preserving XML nodes."""
    rows = table.findall("./w:tr", NAMESPACES)
    positions, people_rows = _collect_people_rows(rows, header_index)
    if not people_rows:
        raise ValueError("La tabla encontrada no contiene filas de personas para ordenar.")
    sorted_rows = [
        row for row, _ in sorted(people_rows, key=lambda item: sort_key(item[1]))
    ]
    replacements = dict(zip(positions, sorted_rows))
    _replace_table_rows(table, rows, replacements)
    return len(sorted_rows)


def _read_document_xml(archive):
    try:
        return archive.read(DOCUMENT_PART)
    except KeyError as error:
        raise ValueError("El archivo no contiene word/document.xml.") from error


def _get_sorted_document_xml(document_xml, sort_key, header):
    root = ET.fromstring(document_xml)
    table, header_index = find_people_table(root, header)
    count = reorder_table_rows(table, header_index, sort_key)
    return _serialize_xml(root, document_xml), count


def _get_output_part_content(archive, entry_info, updated_xml):
    if entry_info.filename == DOCUMENT_PART:
        return updated_xml
    return archive.read(entry_info.filename)


def _write_sorted_archive(archive, temporary_path, updated_xml):
    with zipfile.ZipFile(temporary_path, "w") as output_archive:
        for entry_info in archive.infolist():
            content = _get_output_part_content(archive, entry_info, updated_xml)
            output_archive.writestr(entry_info, content)


def sort_document(input_path, output_path, sort_key, header="Nombre y Apellidos"):
    """Create an ordered DOCX, changing only ``word/document.xml``."""
    output_directory = Path(output_path).resolve().parent
    with zipfile.ZipFile(input_path, "r") as archive:
        document_xml = _read_document_xml(archive)
        updated_xml, count = _get_sorted_document_xml(
            document_xml, sort_key, header
        )
        with tempfile.NamedTemporaryFile(
            mode="wb", suffix=".docx", prefix=TEMPORARY_PREFIX,
            dir=output_directory, delete=False,
        ) as temporary_file:
            temporary_path = Path(temporary_file.name)
        try:
            _write_sorted_archive(archive, temporary_path, updated_xml)
            shutil.move(temporary_path, output_path)
        finally:
            temporary_path.unlink(missing_ok=True)
    return count
