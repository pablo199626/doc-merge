import tempfile
import unittest
from pathlib import Path

from document_operator.pdf_reader import (
    PdfStructureError,
    PdfTextMissingError,
    read_pdf_names,
    parse_pdf_pages,
)
from document_operator.identity import normalize_name


FIXTURE_TEXT = Path(__file__).parent / "fixtures" / "transferencia" / "pagina_pdf.txt"


class PDFReaderTests(unittest.TestCase):
    def test_extracts_only_students_and_preserves_traceability(self):
        result = parse_pdf_pages([FIXTURE_TEXT.read_text(encoding="utf-8")])

        self.assertEqual(result.declared_total, 3)
        self.assertEqual(result.page_count, 1)
        self.assertEqual(
            [entry.original_text for entry in result.entries],
            ["Prueba Norte ANA", "Ensayo Sur LUIS", "Ficción Peña MARÍA ELENA"],
        )
        self.assertNotIn("Ejemplo Responsable MARTA", [entry.original_text for entry in result.entries])
        self.assertEqual(result.entries[0].location, "página 1, entrada 1")

    def test_supports_multiple_pages_repeated_headers_and_wrapped_name(self):
        result = parse_pdf_pages([
            "Alumnado (3)\nNº\nNombre y apellidos\n1\nPrueba Norte ANA\n2\nEnsayo Sur LUIS",
            "Lista de nombres\nAlumnado (3)\nNº\nNombre y apellidos\n3\nFicción Peña\nMARÍA ELENA",
        ])

        self.assertEqual(result.entries[2].original_text, "Ficción Peña MARÍA ELENA")
        self.assertEqual(result.entries[2].page_number, 2)

    def test_retains_duplicate_references(self):
        result = parse_pdf_pages([
            "Alumnado (2)\nNombre y apellidos\n1\nFicción Peña MARÍA\n2\nFiccio\u0301n  Pen\u0303a  Mari\u0301a",
        ])

        self.assertEqual(len(result.entries), 2)
        self.assertEqual(len(result.duplicates), 1)
        self.assertEqual([entry.ordinal for entry in next(iter(result.duplicates.values()))], [1, 2])

    def test_normalization_keeps_accents_and_enye_significant(self):
        self.assertEqual(normalize_name("  FICCIO\u0301N  PEN\u0303A María "), "ficción peña maría")
        self.assertNotEqual(normalize_name("Peña María"), normalize_name("Pena María"))

    def test_rejects_missing_or_partial_extraction(self):
        invalid_pages = [
            "Tutora\nEjemplo Responsable MARTA",
            "Alumnado (2)\nNombre y apellidos\n1\nPrueba Norte ANA",
            "Alumnado (2)\nNombre y apellidos\n1\nPrueba Norte ANA\n3\nEnsayo Sur LUIS",
            "Alumnado (0)\nNombre y apellidos",
        ]
        for text in invalid_pages:
            with self.subTest(text=text):
                with self.assertRaises(PdfStructureError):
                    parse_pdf_pages([text])

    def test_rejects_pdf_without_extractable_text(self):
        with self.assertRaises(PdfTextMissingError):
            parse_pdf_pages(["", "   "])

    def test_rejects_nonexistent_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "No existe"):
                read_pdf_names(Path(temporary) / "lista.pdf")


if __name__ == "__main__":
    unittest.main()
