import hashlib
import tempfile
import unittest
import zipfile
from pathlib import Path

from document_operator.docx_processor import (
    DocxStructureError,
    read_docx_records,
    get_element_text,
)


DOCUMENT_START = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>"""
DOCUMENT_END = "</w:body></w:document>"
HEADER = """<w:tr><w:tc><w:p/></w:tc><w:tc><w:p><w:r><w:t>Nombre y Apellidos</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Medidas</w:t></w:r></w:p></w:tc></w:tr>"""


def person_row(surname, given_name, measures, extra=""):
    return f"""<w:tr>{extra}<w:tc><w:p/></w:tc><w:tc><w:p><w:r><w:t>{surname} </w:t></w:r><w:r><w:t>{given_name}</w:t></w:r></w:p></w:tc><w:tc><w:p><w:pPr><w:pStyle w:val="EstiloOrigen"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="7"/></w:numPr></w:pPr><w:hyperlink r:id="rId9"><w:r><w:rPr><w:b/></w:rPr><w:t>{measures}</w:t></w:r></w:hyperlink></w:p></w:tc></w:tr>"""


def create_docx(path, tables):
    document = DOCUMENT_START + "".join(f"<w:tbl>{rows}</w:tbl>" for rows in tables) + DOCUMENT_END
    relationships = """<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://example.invalid" TargetMode="External"/>
</Relationships>"""
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("word/document.xml", document)
        archive.writestr("word/_rels/document.xml.rels", relationships)


class DOCXReaderTests(unittest.TestCase):
    def test_reads_complete_rows_dependencies_and_auxiliary_rows(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "personas.docx"
            row = person_row("Prueba Norte", "ANA", "MEDIDA-ÚNICA")
            empty = "<w:tr><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>"
            create_docx(path, [HEADER + row + empty])
            before = hashlib.sha256(path.read_bytes()).hexdigest()

            result = read_docx_records(path)

            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), before)
            self.assertEqual(result.sha256, before)
            self.assertEqual(result.table_index, 0)
            self.assertEqual(result.header_index, 0)
            self.assertEqual(result.name_column_index, 1)
            self.assertEqual(len(result.records), 1)
            self.assertEqual(len(result.auxiliary_rows), 1)
            record = result.records[0]
            self.assertEqual(record.original_name, "Prueba Norte ANA")
            self.assertEqual(record.location, "tabla 0, fila 1")
            self.assertIn("MEDIDA-ÚNICA", get_element_text(record.row_xml))
            self.assertEqual(record.dependencies.styles, ("EstiloOrigen",))
            self.assertEqual(record.dependencies.numbering, ("7",))
            self.assertEqual(record.dependencies.relationships[0].relationship_id, "rId9")
            self.assertTrue(record.dependencies.relationships[0].is_external)

    def test_reports_duplicate_people_without_overwriting_them(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "duplicados.docx"
            create_docx(path, [HEADER + person_row("Prueba Norte", "ANA", "A") + person_row("Prueba Norte", "ANA", "B")])

            result = read_docx_records(path)

            duplicate_records = next(iter(result.duplicates.values()))
            self.assertEqual(len(duplicate_records), 2)
            self.assertEqual([record.row_index for record in duplicate_records], [1, 2])

    def test_requires_one_exact_table_header(self):
        with tempfile.TemporaryDirectory() as temporary:
            ambiguous = Path(temporary) / "ambiguo.docx"
            create_docx(ambiguous, [HEADER + person_row("Prueba Norte", "ANA", "A"), HEADER + person_row("Ensayo Sur", "LUIS", "B")])
            with self.assertRaisesRegex(DocxStructureError, "ambigua"):
                read_docx_records(ambiguous)

            inexact = Path(temporary) / "inexacto.docx"
            altered_header = HEADER.replace("Nombre y Apellidos", "Código - Nombre y Apellidos")
            create_docx(inexact, [altered_header + person_row("Prueba Norte", "ANA", "A")])
            with self.assertRaisesRegex(DocxStructureError, "cabecera exacta"):
                read_docx_records(inexact)

    def test_rejects_data_without_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "sin-identidad.docx"
            invalid_row = "<w:tr><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc><w:tc><w:p><w:r><w:t>DATO HUÉRFANO</w:t></w:r></w:p></w:tc></w:tr>"
            create_docx(path, [HEADER + invalid_row])
            with self.assertRaisesRegex(DocxStructureError, "no tiene identidad"):
                read_docx_records(path)

    def test_rejects_unsupported_merged_cells(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "combinada.docx"
            merged = person_row("Prueba Norte", "ANA", "A", "<w:trPr><w:vMerge/></w:trPr>")
            create_docx(path, [HEADER + merged])
            with self.assertRaisesRegex(DocxStructureError, "combinadas verticalmente"):
                read_docx_records(path)


if __name__ == "__main__":
    unittest.main()
