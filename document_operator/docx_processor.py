"""Low-level DOCX processing that preserves every document element.

A DOCX is a ZIP package. This module copies every package entry unchanged and
only reorders complete ``w:tr`` XML nodes in ``word/document.xml``.
"""

import shutil
import tempfile
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET


WORD_NAMESPACE = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NAMESPACES = {"w": WORD_NAMESPACE}
W_TR = f"{{{WORD_NAMESPACE}}}tr"


def texto_elemento(elemento):
    """Return visible text from all text runs inside a Word XML element."""
    return "".join(nodo.text or "" for nodo in elemento.findall(".//w:t", NAMESPACES))


def celdas_fila(fila):
    """Return the direct table cells of a row."""
    return fila.findall("./w:tc", NAMESPACES)


def encontrar_tabla_personas(raiz, cabecera="Nombre y Apellidos"):
    """Locate the table and header row containing the configured header text."""
    for tabla in raiz.findall(".//w:tbl", NAMESPACES):
        for indice, fila in enumerate(tabla.findall("./w:tr", NAMESPACES)):
            if any(cabecera in texto_elemento(celda) for celda in celdas_fila(fila)):
                return tabla, indice
    raise ValueError(f'No se encontró una tabla con la cabecera "{cabecera}".')


def reordenar_filas_tabla(tabla, indice_cabecera, clave_ordenacion):
    """Reorder non-empty person rows after a header, preserving XML nodes."""
    filas = tabla.findall("./w:tr", NAMESPACES)
    posiciones = []
    filas_personas = []

    for indice, fila in enumerate(filas[indice_cabecera + 1:], start=indice_cabecera + 1):
        celdas = celdas_fila(fila)
        if len(celdas) < 2:
            continue
        nombre = texto_elemento(celdas[1]).strip()
        if nombre:
            posiciones.append(indice)
            filas_personas.append((fila, nombre))

    if not filas_personas:
        raise ValueError("La tabla encontrada no contiene filas de personas para ordenar.")

    filas_ordenadas = [fila for fila, _ in sorted(filas_personas, key=lambda item: clave_ordenacion(item[1]))]
    reemplazos = dict(zip(posiciones, filas_ordenadas))
    secuencia_final = [reemplazos.get(indice, fila) for indice, fila in enumerate(filas)]

    hijos_tabla = list(tabla)
    indice_primera_fila = next(indice for indice, hijo in enumerate(hijos_tabla) if hijo.tag == W_TR)
    for fila in filas:
        tabla.remove(fila)
    for desplazamiento, fila in enumerate(secuencia_final):
        tabla.insert(indice_primera_fila + desplazamiento, fila)

    return len(filas_ordenadas)


def ordenar_documento(archivo_entrada, archivo_salida, clave_ordenacion, cabecera="Nombre y Apellidos"):
    """Create an ordered DOCX, changing only ``word/document.xml``."""
    with zipfile.ZipFile(archivo_entrada, "r") as archivo_zip:
        try:
            documento_xml = archivo_zip.read("word/document.xml")
        except KeyError as error:
            raise ValueError("El archivo no contiene word/document.xml.") from error

        raiz = ET.fromstring(documento_xml)
        tabla, indice_cabecera = encontrar_tabla_personas(raiz, cabecera)
        cantidad = reordenar_filas_tabla(tabla, indice_cabecera, clave_ordenacion)
        xml_actualizado = ET.tostring(raiz, encoding="utf-8", xml_declaration=True)

        directorio_salida = Path(archivo_salida).resolve().parent
        with tempfile.NamedTemporaryFile(
            mode="wb", suffix=".docx", prefix="document_operator_", dir=directorio_salida, delete=False
        ) as temporal:
            ruta_temporal = Path(temporal.name)

        try:
            with zipfile.ZipFile(ruta_temporal, "w") as salida_zip:
                for info in archivo_zip.infolist():
                    contenido = xml_actualizado if info.filename == "word/document.xml" else archivo_zip.read(info.filename)
                    salida_zip.writestr(info, contenido)
            shutil.move(ruta_temporal, archivo_salida)
        finally:
            ruta_temporal.unlink(missing_ok=True)

    return cantidad
