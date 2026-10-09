"""High-level, transactional transfer workflow."""

import os
import tempfile
from contextlib import contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterator, Optional, Tuple

from document_operator.docx_processor import (
    DocxTransferRequest,
    apply_transfer_plan,
    read_docx_records,
    sort_document,
)
from document_operator.matching import TransferPlan, create_transfer_plan
from document_operator.pdf_reader import read_pdf_names
from document_operator.validation import (
    TransferValidationRequest,
    verify_file_hash,
    validate_transfer_result,
)


DEFAULT_HEADER = "Nombre y Apellidos"


@dataclass(frozen=True)
class TransferInputs:
    source_path: Path
    destination_path: Path
    list_path: Path
    header: str = DEFAULT_HEADER


@dataclass(frozen=True)
class TransferRequest:
    inputs: TransferInputs
    output_path: Path
    sort_key: Callable[[str], Tuple[str, str, str]]
    plan: Optional[TransferPlan] = None


def prepare_transfer(inputs: TransferInputs) -> TransferPlan:
    pdf_result = read_pdf_names(inputs.list_path)
    source = read_docx_records(inputs.source_path, inputs.header)
    destination = read_docx_records(inputs.destination_path, inputs.header)
    return create_transfer_plan(pdf_result, source, destination)


def _create_temporary_path(directory: Path, label: str) -> Path:
    descriptor, name = tempfile.mkstemp(
        suffix=f"_{label}.docx",
        prefix="document_operator_",
        dir=directory,
    )
    os.close(descriptor)
    path = Path(name)
    path.unlink()
    return path


@contextmanager
def _temporary_transfer_paths(directory: Path) -> Iterator[Tuple[Path, Path]]:
    paths = (
        _create_temporary_path(directory, "transferido"),
        _create_temporary_path(directory, "ordenado"),
    )
    try:
        yield paths
    finally:
        for path in paths:
            path.unlink(missing_ok=True)


def _verify_inputs(inputs: TransferInputs, plan: TransferPlan) -> None:
    verify_file_hash(inputs.source_path, plan.source_hash, "de origen")
    verify_file_hash(inputs.destination_path, plan.destination_hash, "de destino")
    verify_file_hash(inputs.list_path, plan.list_hash, "PDF")


def execute_transfer(request: TransferRequest) -> TransferPlan:
    """Generate, sort and validate temporary files before atomic publication."""
    plan = request.plan or prepare_transfer(request.inputs)
    output_path = Path(request.output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with _temporary_transfer_paths(output_path.parent) as temporary_paths:
        transferred_path, sorted_path = temporary_paths
        apply_transfer_plan(DocxTransferRequest(
            request.inputs.source_path,
            request.inputs.destination_path,
            transferred_path,
            plan,
            request.inputs.header,
        ))
        sort_document(
            transferred_path,
            sorted_path,
            request.sort_key,
            request.inputs.header,
        )
        _verify_inputs(request.inputs, plan)
        validate_transfer_result(TransferValidationRequest(
            sorted_path,
            request.inputs.destination_path,
            plan,
            request.sort_key,
            request.inputs.header,
        ))
        os.replace(sorted_path, output_path)
    return plan
