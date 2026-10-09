import hashlib
import tempfile
import unittest
import zipfile
from unittest.mock import patch
from pathlib import Path

from document_operator.docx_processor import (
    DocxTransferRequest,
    RELATIONSHIP_NAMESPACE,
    apply_transfer_plan,
    read_docx_records,
)
from document_operator.identity import normalize_name
from document_operator.matching import create_transfer_plan
from document_operator.pdf_reader import PdfEntry, PdfReadResult
from document_operator.sorting import get_sort_rule
from document_operator.transfer import TransferInputs, TransferRequest, execute_transfer
from document_operator.validation import TransferValidationError


W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


def create_package(path, person, marker, source=False):
    style_color = "FF0000" if source else "0000FF"
    measure = "INFORMACIÓN-ORIGEN" if source else "INFORMACIÓN-DESTINO"
    relationships = """<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://%s.invalid" TargetMode="External"/>
%s
</Relationships>""" % (
        "source" if source else "destination",
        '<Relationship Id="rId10" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/source.bin"/>' if source else "",
    )
    resource = '<w:drawing r:embed="rId10"/>' if source else ""
    document = f"""<?xml version="1.0" encoding="UTF-8"?>
<w:document xmlns:w="{W}" xmlns:r="{RELATIONSHIP_NAMESPACE}"><w:body>
<w:p><w:r><w:t>{marker}</w:t></w:r></w:p>
<w:tbl>
<w:tr><w:tc><w:p/></w:tc><w:tc><w:p><w:r><w:t>Nombre y Apellidos</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Medidas</w:t></w:r></w:p></w:tc></w:tr>
<w:tr><w:tc><w:p/></w:tc><w:tc><w:p><w:pPr><w:pStyle w:val="EstiloPersona"/></w:pPr><w:r><w:t>{person}</w:t></w:r></w:p></w:tc><w:tc><w:p><w:pPr><w:pStyle w:val="EstiloPersona"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="7"/></w:numPr></w:pPr><w:hyperlink r:id="rId9"><w:r><w:t>{measure}</w:t></w:r></w:hyperlink>{resource}</w:p></w:tc></w:tr>
</w:tbl><w:sectPr/></w:body></w:document>"""
    styles = f"""<?xml version="1.0" encoding="UTF-8"?>
<w:styles xmlns:w="{W}"><w:style w:type="paragraph" w:styleId="Normal"><w:name w:val="Normal"/><w:rPr><w:color w:val="{style_color}"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="EstiloPersona"><w:name w:val="Persona"/><w:basedOn w:val="Normal"/><w:pPr><w:numPr><w:numId w:val="7"/></w:numPr></w:pPr></w:style></w:styles>"""
    numbering = f"""<?xml version="1.0" encoding="UTF-8"?>
<w:numbering xmlns:w="{W}"><w:abstractNum w:abstractNumId="1"><w:lvl w:ilvl="0"><w:numFmt w:val="decimal"/><w:lvlText w:val="{'%1.' if source else '(%1)'}"/></w:lvl></w:abstractNum><w:num w:numId="7"><w:abstractNumId w:val="1"/></w:num></w:numbering>"""
    content_types = """<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>%s</Types>""" % (
        '<Default Extension="bin" ContentType="application/octet-stream"/>' if source else ""
    )
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("[Content_Types].xml", content_types)
        archive.writestr("word/document.xml", document)
        archive.writestr("word/styles.xml", styles)
        archive.writestr("word/numbering.xml", numbering)
        archive.writestr("word/_rels/document.xml.rels", relationships)
        archive.writestr("custom/marker.txt", marker)
        if source:
            archive.writestr("word/media/source.bin", b"SOURCE-RESOURCE")


def pdf_for(name):
    entry = PdfEntry(name, normalize_name(name), 1, 1)
    return PdfReadResult((entry,), 1, 1)


class TransferTests(unittest.TestCase):
    def test_transfers_complete_row_format_numbering_and_resources(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            output = root / "output.docx"
            create_package(source, "Prueba Norte ANA", "MARCADOR-ORIGEN", source=True)
            create_package(destination, "Ensayo Sur LUIS", "MARCADOR-DESTINO")
            hashes = {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in (source, destination)}
            source_data = read_docx_records(source)
            destination_data = read_docx_records(destination)
            plan = create_transfer_plan(pdf_for("Prueba Norte ANA"), source_data, destination_data)

            apply_transfer_plan(DocxTransferRequest(source, destination, output, plan))

            self.assertEqual(
                {path: hashlib.sha256(path.read_bytes()).hexdigest() for path in (source, destination)}, hashes
            )
            result = read_docx_records(output)
            self.assertEqual([record.original_name for record in result.records], ["Prueba Norte ANA"])
            record = result.records[0]
            self.assertIn("INFORMACIÓN-ORIGEN", "".join(record.row_xml.itertext()))
            self.assertTrue(record.dependencies.styles[0].startswith("DO_EstiloPersona"))
            self.assertNotEqual(record.dependencies.numbering, ("7",))
            with zipfile.ZipFile(output) as archive:
                self.assertEqual(archive.read("custom/marker.txt"), b"MARCADOR-DESTINO")
                imported = [name for name in archive.namelist() if name.startswith("word/media/")]
                self.assertEqual(len(imported), 1)
                self.assertEqual(archive.read(imported[0]), b"SOURCE-RESOURCE")
                self.assertIn(b'MARCADOR-DESTINO', archive.read("word/document.xml"))
                self.assertNotIn(b'MARCADOR-ORIGEN', archive.read("word/document.xml"))

    def test_replaces_existing_person_without_duplicating_it(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            output = root / "output.docx"
            create_package(source, "Prueba Norte ANA", "ORIGEN", source=True)
            create_package(destination, "Prueba Norte ANA", "DESTINO")
            plan = create_transfer_plan(
                pdf_for("Prueba Norte ANA"), read_docx_records(source), read_docx_records(destination)
            )

            apply_transfer_plan(DocxTransferRequest(source, destination, output, plan))

            result = read_docx_records(output)
            self.assertEqual(len(result.records), 1)
            self.assertIn("INFORMACIÓN-ORIGEN", "".join(result.records[0].row_xml.itertext()))

    def test_transactional_workflow_validates_before_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            output = root / "output.docx"
            create_package(source, "Prueba Norte ANA", "ORIGEN", source=True)
            create_package(destination, "Ensayo Sur LUIS", "DESTINO")
            plan = create_transfer_plan(
                pdf_for("Prueba Norte ANA"), read_docx_records(source), read_docx_records(destination)
            )

            execute_transfer(
                TransferRequest(
                    TransferInputs(source, destination, root / "unused.pdf"),
                    output,
                    get_sort_rule("apellido-nombre-segundo-apellido"),
                    plan,
                )
            )

            self.assertEqual([r.original_name for r in read_docx_records(output).records], ["Prueba Norte ANA"])

    def test_validation_failure_preserves_previous_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.docx"
            destination = root / "destination.docx"
            output = root / "output.docx"
            create_package(source, "Prueba Norte ANA", "ORIGEN", source=True)
            create_package(destination, "Ensayo Sur LUIS", "DESTINO")
            output.write_bytes(b"SALIDA-ANTERIOR")
            plan = create_transfer_plan(
                pdf_for("Prueba Norte ANA"), read_docx_records(source), read_docx_records(destination)
            )

            with patch("document_operator.transfer.validate_transfer_result", side_effect=TransferValidationError("fallo")):
                with self.assertRaises(TransferValidationError):
                    execute_transfer(
                        TransferRequest(
                            TransferInputs(source, destination, root / "unused.pdf"),
                            output,
                            get_sort_rule("apellido-nombre-segundo-apellido"),
                            plan,
                        )
                    )
            self.assertEqual(output.read_bytes(), b"SALIDA-ANTERIOR")


if __name__ == "__main__":
    unittest.main()
