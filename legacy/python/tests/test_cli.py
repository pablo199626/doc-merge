import io
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch

from document_operator.cli import main, default_transfer_output_path
from document_operator.matching import TransferIssue, TransferPlan
from document_operator.transfer_types import IssueType


class CLITests(unittest.TestCase):
    def test_transfer_output_name(self):
        self.assertEqual(
            default_transfer_output_path(Path("grupo A.docx")),
            Path("grupo A_generado.docx"),
        )

    def test_simulation_prints_actions_and_does_not_generate(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            listing = root / "listing.pdf"
            for path in (source, destination, listing):
                path.write_bytes(b"placeholder")
            plan = TransferPlan(
                (), (TransferIssue(IssueType.MISSING_SOURCE, "x", "Persona X", "No encontrada", ("página 1",)),),
                "", "", "", 1,
            )
            arguments = [
                "document-operator", str(source), "--destino", str(destination),
                "--lista", str(listing), "--simular",
            ]
            output = io.StringIO()
            with patch("sys.argv", arguments), patch(
                "document_operator.cli.prepare_transfer", return_value=plan
            ), patch("document_operator.cli.execute_transfer") as execute, redirect_stdout(output):
                main()

            execute.assert_not_called()
            self.assertIn("Simulación de transferencia", output.getvalue())
            self.assertIn("Persona X", output.getvalue())
            self.assertFalse((root / "destination_generado.docx").exists())

    def test_requires_destination_and_list_together(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.docx"
            source.write_bytes(b"placeholder")
            with patch("sys.argv", ["document-operator", str(source), "--destino", str(source)]):
                with self.assertRaises(SystemExit) as error:
                    main()
            self.assertEqual(error.exception.code, 2)

    def test_double_click_wizard_can_simulate_transfer(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            listing = root / "listing.pdf"
            for path in (source, destination, listing):
                path.write_bytes(b"placeholder")
            plan = TransferPlan((), (), "", "", "", 0)
            answers = [
                "1", str(source), str(destination), str(listing), "", "s", "",
            ]
            output = io.StringIO()
            with patch("sys.argv", ["document-operator"]), patch(
                "builtins.input", side_effect=answers
            ), patch(
                "document_operator.cli.prepare_transfer", return_value=plan
            ), patch("document_operator.cli.execute_transfer") as execute, redirect_stdout(output):
                main()

            execute.assert_not_called()
            self.assertIn("Transferir personas", output.getvalue())
            self.assertIn("Simulación de transferencia", output.getvalue())


if __name__ == "__main__":
    unittest.main()
