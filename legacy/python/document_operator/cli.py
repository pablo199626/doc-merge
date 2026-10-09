"""Command-line and double-click interfaces for Document Operator."""

import argparse
import sys
import zipfile
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Optional
from xml.etree import ElementTree as ET

from document_operator import __version__
from document_operator.docx_processor import sort_document
from document_operator.matching import (
    TransferPlan,
    format_execution_summary,
    format_issue_summary,
    format_simulation_summary,
)
from document_operator.sorting import SORT_RULES, get_sort_rule
from document_operator.transfer import (
    DEFAULT_HEADER,
    TransferInputs,
    TransferRequest,
    execute_transfer,
    prepare_transfer,
)


DOCX_EXTENSION = ".docx"
PDF_EXTENSION = ".pdf"
TRANSFER_OPTION = "1"
SORT_OPTION = "2"
AFFIRMATIVE_ANSWERS = frozenset({"s", "si", "sí"})


class CliUsageError(ValueError):
    """The supplied command options are incomplete or unsafe."""


@dataclass(frozen=True)
class CliOptions:
    source: Optional[str]
    output: Optional[str]
    destination: Optional[str]
    pdf_list: Optional[str]
    simulate: bool
    sort_rule: str
    header: str
    force: bool
    is_interactive: bool = False


@dataclass(frozen=True)
class ExecutionContext:
    options: CliOptions
    source_path: Path
    output_path: Path
    transfer_inputs: Optional[TransferInputs]

    @property
    def is_transfer(self) -> bool:
        return self.transfer_inputs is not None


def default_sort_output_path(input_path: Path) -> Path:
    """Return ``<name>_ordenado.docx`` beside the input file."""
    path = Path(input_path)
    return path.with_name(f"{path.stem}_ordenado{path.suffix}")


def default_transfer_output_path(destination_path: Path) -> Path:
    path = Path(destination_path)
    return path.with_name(f"{path.stem}_generado{path.suffix}")


def _add_transfer_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--destino",
        dest="destination",
        help="DOCX que sirve de base para la transferencia",
    )
    parser.add_argument(
        "--lista", dest="pdf_list", help="PDF que determina las personas del resultado"
    )
    parser.add_argument(
        "--simular",
        dest="simulate",
        action="store_true",
        help="Muestra las acciones de transferencia sin generar ningún documento",
    )


def _add_sort_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument(
        "--orden",
        dest="sort_rule",
        default="apellido-nombre-segundo-apellido",
        choices=sorted(SORT_RULES),
        help="Regla de ordenación (predeterminada: %(default)s)",
    )
    parser.add_argument(
        "--cabecera",
        dest="header",
        default=DEFAULT_HEADER,
        help="Texto que identifica la cabecera de la tabla objetivo",
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="document-operator",
        description=(
            "Ordena tablas Word o transfiere las personas indicadas por un listado PDF."
        ),
    )
    parser.add_argument("source", nargs="?", help="Documento Word (.docx) de entrada")
    parser.add_argument("-o", "--output", help="Ruta del documento ordenado")
    _add_transfer_arguments(parser)
    _add_sort_arguments(parser)
    parser.add_argument(
        "--force",
        action="store_true",
        help="Sobrescribe la salida existente sin preguntar",
    )
    parser.add_argument(
        "--version", action="version", version=f"document-operator {__version__}"
    )
    return parser


def _clean_path(value: str) -> str:
    return value.strip().strip('"').strip("'")


def _options_from_namespace(namespace: argparse.Namespace) -> CliOptions:
    return CliOptions(
        namespace.source,
        namespace.output,
        namespace.destination,
        namespace.pdf_list,
        namespace.simulate,
        namespace.sort_rule,
        namespace.header,
        namespace.force,
    )


def _show_wizard_header() -> None:
    print(f"DOCUMENT OPERATOR {__version__}")
    print("=======================")
    print("1. Transferir personas usando un listado PDF")
    print("2. Ordenar alfabéticamente un documento Word")


def _ask_optional_output() -> Optional[str]:
    output = _clean_path(
        input("Ruta de salida (Intro para usar la predeterminada): ")
    )
    return output or None


def _run_double_click_wizard(options: CliOptions) -> CliOptions:
    _show_wizard_header()
    selected_option = input("\nElige una opción [1]: ").strip() or TRANSFER_OPTION
    if selected_option not in {TRANSFER_OPTION, SORT_OPTION}:
        raise CliUsageError("la opción debe ser 1 o 2")
    source = _clean_path(input("Ruta del documento Word de origen: "))
    if selected_option == SORT_OPTION:
        return replace(
            options,
            source=source,
            output=_ask_optional_output(),
            is_interactive=True,
        )
    destination = _clean_path(input("Ruta del documento Word de destino: "))
    pdf_list = _clean_path(input("Ruta del listado PDF: "))
    output = _ask_optional_output()
    answer = input(
        "¿Solo simular sin crear el documento? (s/n) [n]: "
    ).strip().casefold()
    return replace(
        options,
        source=source,
        output=output,
        destination=destination,
        pdf_list=pdf_list,
        simulate=answer in AFFIRMATIVE_ANSWERS,
        is_interactive=True,
    )


def _require_existing_file(path: Path, extension: str, description: str) -> None:
    if path.suffix.casefold() != extension or not path.is_file():
        raise CliUsageError(f"{description}: {path}")


def _build_transfer_inputs(options: CliOptions) -> Optional[TransferInputs]:
    has_transfer_option = options.destination is not None or options.pdf_list is not None
    if not has_transfer_option:
        if options.simulate:
            raise CliUsageError(
                "--simular solo está disponible junto con --destino y --lista"
            )
        return None
    if not options.destination or not options.pdf_list:
        raise CliUsageError("--destino y --lista deben indicarse conjuntamente")
    destination_path = Path(options.destination)
    list_path = Path(options.pdf_list)
    _require_existing_file(
        destination_path, DOCX_EXTENSION, "el destino no es un DOCX existente"
    )
    _require_existing_file(
        list_path, PDF_EXTENSION, "el listado no es un PDF existente"
    )
    return TransferInputs(
        Path(options.source), destination_path, list_path, options.header
    )


def _select_output_path(
    options: CliOptions,
    source_path: Path,
    transfer_inputs: Optional[TransferInputs],
) -> Path:
    if options.output:
        return Path(options.output)
    if transfer_inputs is not None:
        return default_transfer_output_path(transfer_inputs.destination_path)
    return default_sort_output_path(source_path)


def _validate_distinct_output(context: ExecutionContext) -> None:
    input_paths = [context.source_path]
    if context.transfer_inputs is not None:
        input_paths.extend((
            context.transfer_inputs.destination_path,
            context.transfer_inputs.list_path,
        ))
    if any(context.output_path.resolve() == path.resolve() for path in input_paths):
        raise CliUsageError(
            "el archivo de salida debe ser distinto de todos los archivos de entrada"
        )


def _build_execution_context(options: CliOptions) -> ExecutionContext:
    if not options.source:
        raise CliUsageError("debes indicar un documento de entrada")
    source_path = Path(options.source)
    _require_existing_file(
        source_path, DOCX_EXTENSION, "el archivo de entrada no es un DOCX existente"
    )
    transfer_inputs = _build_transfer_inputs(options)
    output_path = _select_output_path(options, source_path, transfer_inputs)
    if output_path.suffix.casefold() != DOCX_EXTENSION:
        raise CliUsageError("el archivo de salida debe tener extensión .docx")
    context = ExecutionContext(options, source_path, output_path, transfer_inputs)
    _validate_distinct_output(context)
    return context


def _confirm_overwrite(context: ExecutionContext) -> bool:
    if context.options.simulate or not context.output_path.exists():
        return True
    if context.options.force:
        return True
    if not context.options.is_interactive:
        raise CliUsageError(
            f"ya existe el archivo de salida: {context.output_path}. "
            "Usa --force para sobrescribirlo"
        )
    answer = input(f"{context.output_path} ya existe. ¿Sobrescribir? (s/n): ")
    return answer.strip().casefold() in AFFIRMATIVE_ANSWERS


def _execute_transfer(context: ExecutionContext) -> Optional[TransferPlan]:
    plan = prepare_transfer(context.transfer_inputs)
    if context.options.simulate:
        print(format_simulation_summary(plan))
        print(format_issue_summary(plan))
        return None
    execute_transfer(TransferRequest(
        context.transfer_inputs,
        context.output_path,
        get_sort_rule(context.options.sort_rule),
        plan,
    ))
    print(f"Documento creado: {context.output_path}")
    print(format_execution_summary(plan))
    print(format_issue_summary(plan))
    return plan


def _execute_sort(context: ExecutionContext) -> None:
    count = sort_document(
        context.source_path,
        context.output_path,
        get_sort_rule(context.options.sort_rule),
        context.options.header,
    )
    print(f"Documento creado: {context.output_path}")
    print(f"Filas ordenadas: {count}")


def _execute_context(context: ExecutionContext) -> None:
    if not _confirm_overwrite(context):
        print("Operación cancelada.")
        return
    if context.is_transfer:
        _execute_transfer(context)
        return
    _execute_sort(context)


def _pause() -> None:
    input("\nPulsa Intro para cerrar...")


def main() -> None:
    parser = build_parser()
    namespace = parser.parse_args()
    options = _options_from_namespace(namespace)
    is_double_click = len(sys.argv) == 1
    try:
        if is_double_click:
            options = _run_double_click_wizard(options)
        context = _build_execution_context(options)
        _execute_context(context)
    except CliUsageError as error:
        if not is_double_click:
            parser.error(str(error))
        print(f"Error: {error}", file=sys.stderr)
    except (OSError, ValueError, ET.ParseError, zipfile.BadZipFile) as error:
        print(f"Error: {error}", file=sys.stderr)
        if not is_double_click:
            raise SystemExit(1) from error
    finally:
        if is_double_click:
            _pause()
