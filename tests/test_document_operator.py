import shutil
import tempfile
import unittest
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from document_operator.docx_processor import (
    NAMESPACES,
    celdas_fila,
    encontrar_tabla_personas,
    ordenar_documento,
    texto_elemento,
)
from document_operator.sorting import obtener_regla


PROJECT_ROOT = Path(__file__).resolve().parents[1]
FIXTURE = PROJECT_ROOT / "PAABLO ANEXOS.docx"


class DocumentOperatorIntegrationTests(unittest.TestCase):
    def test_orders_people_and_changes_only_document_xml(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "resultado.docx"
            count = ordenar_documento(
                FIXTURE,
                output,
                obtener_regla("apellido-nombre-segundo-apellido"),
            )
            self.assertEqual(count, 10)

            with zipfile.ZipFile(FIXTURE) as source, zipfile.ZipFile(output) as result:
                self.assertEqual(set(source.namelist()), set(result.namelist()))
                changed = [
                    name for name in source.namelist()
                    if source.read(name) != result.read(name)
                ]
            self.assertEqual(changed, ["word/document.xml"])

            with zipfile.ZipFile(output) as result:
                root = ET.fromstring(result.read("word/document.xml"))
            table, header_index = encontrar_tabla_personas(root)
            names = [
                texto_elemento(celdas_fila(row)[1]).strip()
                for row in table.findall("./w:tr", NAMESPACES)[header_index + 1:]
            ]
            self.assertEqual(names, [
                "Garcia Garcia MANOLETA",
                "Garcia Taboada MANOLO",
                "Garcia Lopez PEPITO",
                "Gonzalez Perez Alvaro",
                "Lopez Navarro AZNAR",
                "Rubio Garcia AZNAR",
                "Rubio Olmedo PEPE",
                "Sanchez Lopez Alberto",
                "Sanchez Garcia AZNAR",
                "Taboada Sanchez Maria",
            ])

    def test_does_not_modify_source(self):
        with tempfile.TemporaryDirectory() as temporary:
            source_copy = Path(temporary) / "entrada.docx"
            output = Path(temporary) / "salida.docx"
            shutil.copy2(FIXTURE, source_copy)
            before = source_copy.read_bytes()
            ordenar_documento(source_copy, output, obtener_regla("nombre-apellidos"))
            self.assertEqual(source_copy.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
