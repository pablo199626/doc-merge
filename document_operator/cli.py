"""Command-line interface for Document Operator."""

import argparse
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from document_operator import __version__
from document_operator.docx_processor import ordenar_documento
from document_operator.sorting import REGLAS_ORDENACION, obtener_regla


def ruta_salida_predeterminada(archivo_entrada):
    """Return ``<name>_ordenado.docx`` beside the input file."""
    ruta = Path(archivo_entrada)
    return ruta.with_name(f"{ruta.stem}_ordenado{ruta.suffix}")


def crear_parser():
    parser = argparse.ArgumentParser(
        prog="document-operator",
        description="Ordena una tabla Word y conserva el formato completo del documento.",
    )
    parser.add_argument("archivo", nargs="?", help="Documento Word (.docx) de entrada")
    parser.add_argument("-o", "--output", help="Ruta del documento ordenado")
    parser.add_argument(
        "--orden",
        default="apellido-nombre-segundo-apellido",
        choices=sorted(REGLAS_ORDENACION),
        help="Regla de ordenación (predeterminada: %(default)s)",
    )
    parser.add_argument(
        "--cabecera",
        default="Nombre y Apellidos",
        help="Texto que identifica la cabecera de la tabla objetivo",
    )
    parser.add_argument("--force", action="store_true", help="Sobrescribe la salida existente sin preguntar")
    parser.add_argument("--version", action="version", version=f"document-operator {__version__}")
    return parser


def main():
    parser = crear_parser()
    argumentos = parser.parse_args()

    archivo_entrada = argumentos.archivo or input("Ruta del documento Word original: ").strip()
    if not archivo_entrada:
        parser.error("debes indicar un documento de entrada")

    ruta_entrada = Path(archivo_entrada)
    if ruta_entrada.suffix.lower() != ".docx":
        parser.error("el archivo de entrada debe tener extensión .docx")
    if not ruta_entrada.is_file():
        parser.error(f"no existe el archivo: {ruta_entrada}")

    ruta_salida = Path(argumentos.output) if argumentos.output else ruta_salida_predeterminada(ruta_entrada)
    if ruta_salida.resolve() == ruta_entrada.resolve():
        parser.error("el archivo de salida debe ser distinto del original")
    if ruta_salida.exists() and not argumentos.force:
        if argumentos.archivo:
            parser.error(f"ya existe el archivo de salida: {ruta_salida}. Usa --force para sobrescribirlo")
        respuesta = input(f"{ruta_salida} ya existe. ¿Sobrescribir? (s/n): ").strip().casefold()
        if respuesta not in {"s", "si", "sí"}:
            print("Operación cancelada.")
            return

    try:
        cantidad = ordenar_documento(
            ruta_entrada,
            ruta_salida,
            obtener_regla(argumentos.orden),
            argumentos.cabecera,
        )
    except (OSError, ValueError, ET.ParseError, zipfile.BadZipFile) as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)

    print(f"Documento creado: {ruta_salida}")
    print(f"Filas ordenadas: {cantidad}")
