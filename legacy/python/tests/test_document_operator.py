import shutil
import re
import tempfile
import unittest
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from document_operator.docx_processor import (
    NAMESPACES,
    get_row_cells,
    find_people_table,
    sort_document,
    get_element_text,
)
from document_operator.sorting import get_sort_rule


PROJECT_ROOT = Path(__file__).resolve().parents[1]
FIXTURE = PROJECT_ROOT / "PAABLO ANEXOS.docx"


class DocumentOperatorIntegrationTests(unittest.TestCase):
    def test_orders_people_and_changes_only_document_xml(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "resultado.docx"
            count = sort_document(
                FIXTURE,
                output,
                get_sort_rule("apellido-nombre-segundo-apellido"),
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
                document_xml = result.read("word/document.xml")
                root = ET.fromstring(document_xml)
            ignorable = re.search(rb'Ignorable="([^"]+)"', document_xml)
            if ignorable:
                for prefix in ignorable.group(1).split():
                    self.assertIn(b"xmlns:" + prefix + b"=", document_xml)
            table, header_index = find_people_table(root)
            names = [
                get_element_text(get_row_cells(row)[1]).strip()
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
            sort_document(source_copy, output, get_sort_rule("nombre-apellidos"))
            self.assertEqual(source_copy.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
