import json
import unittest
from pathlib import Path
from xml.etree import ElementTree as ET

from document_operator.docx_processor import (
    RecordDependencies,
    DocxRecord,
    DocxReadResult,
)
from document_operator.identity import normalize_name
from document_operator.matching import create_transfer_plan, format_issue_summary, format_execution_summary
from document_operator.pdf_reader import PdfEntry, PdfReadResult
from document_operator.transfer_types import IssueType


CASES = Path(__file__).parent / "fixtures" / "transferencia" / "casos.json"


def docx_result(records, source):
    converted = tuple(
        DocxRecord(
            Path(source), 0, index + 1, record["nombre"],
            normalize_name(record["nombre"]),
            ET.fromstring(f"<row><name>{record['nombre']}</name><data>{'|'.join(record['medidas'])}</data></row>"),
            RecordDependencies((), (), ()),
        )
        for index, record in enumerate(records)
    )
    return DocxReadResult(Path(source), source, 0, 0, 1, converted, ())


def pdf_result(names):
    entries = tuple(
        PdfEntry(name, normalize_name(name), 1, index + 1)
        for index, name in enumerate(names)
    )
    return PdfReadResult(entries, len(entries), 1)


class MatchingTests(unittest.TestCase):
    def test_all_acceptance_cases(self):
        cases = json.loads(CASES.read_text(encoding="utf-8"))["casos"]
        for case in cases:
            with self.subTest(case=case["id"]):
                origin = docx_result(case["origen"], "origin")
                destination = docx_result(case["destino"], "destination")
                plan = create_transfer_plan(pdf_result(case["pdf"]), origin, destination)
                actual = []
                for action in plan.actions:
                    action_names = {
                        "add": "anadir", "replace": "reemplazar", "keep": "conservar",
                        "delete": "eliminar", "skip": "omitir",
                    }
                    item = {"tipo": action_names[action.action_type.value]}
                    if action.source:
                        item["origen"] = case["origen"][action.source.row_index - 1]["id"]
                    if action.destination:
                        item["destino"] = case["destino"][action.destination.row_index - 1]["id"]
                    if action.reason:
                        item["motivo"] = action.reason.value
                    actual.append(item)
                self.assertEqual(actual, case["acciones"])

    def test_summary_has_actions_counts_and_reasons(self):
        plan = create_transfer_plan(
            pdf_result(["Prueba Norte ANA"]), docx_result([], "origin"),
            docx_result([{"nombre": "Prueba Norte ANA", "medidas": ["A"]}], "destination"),
        )
        summary = format_execution_summary(plan)
        self.assertIn("Conservar: Prueba Norte ANA (origen_ausente)", summary)
        self.assertIn("conservar=1", summary)
        self.assertEqual(plan.issues[0].issue_type, IssueType.MISSING_SOURCE)
        self.assertIn("página 1, entrada 1", format_issue_summary(plan))


if __name__ == "__main__":
    unittest.main()
