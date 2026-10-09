using DocMerge.Application.Documents;
using DocMerge.Application.Validation;
using DocMerge.Domain.Pdf;
using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;
using DocMerge.Infrastructure.Docx;

namespace DocMerge.Infrastructure.Tests.Docx;

public sealed class DocxDocumentEditorTests
{
    [Fact]
    public async Task SortAsync_PeopleRows_ChangesOnlyDocumentPart()
    {
        using var directory = new TemporaryDirectory();
        var input = directory.File("input.docx");
        var output = directory.File("output.docx");
        DocxTestPackage.Create(input, [DocxTestPackage.Header
            + DocxTestPackage.PersonRow("Zulu Norte ANA", "A")
            + DocxTestPackage.PersonRow("Alpha Norte ANA", "B")]);
        var editor = new DocxDocumentEditor();

        var request = new SortDocumentRequest(
            input, output, SortingProfiles.Get(SortingProfiles.DefaultIdentifier),
            "Nombre y Apellidos");
        var count = await editor.SortAsync(request, CancellationToken.None);

        Assert.Equal(2, count);
        var before = DocxTestPackage.ReadParts(input);
        var after = DocxTestPackage.ReadParts(output);
        Assert.Equal(before.Keys, after.Keys);
        Assert.Equal(
            ["word/document.xml"],
            before.Keys.Where(name => !before[name].SequenceEqual(after[name])));
    }

    [Fact]
    public async Task SortAsync_HeaderInFirstColumn_DerivesIdentityColumn()
    {
        using var directory = new TemporaryDirectory();
        var input = directory.File("input.docx");
        var output = directory.File("output.docx");
        const string header = "<w:tr><w:tc><w:p><w:r><w:t>Nombre y Apellidos</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>";
        const string zulu = "<w:tr><w:tc><w:p><w:r><w:t>Zulu Norte ANA</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>";
        const string alpha = "<w:tr><w:tc><w:p><w:r><w:t>Alpha Norte ANA</w:t></w:r></w:p></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>";
        DocxTestPackage.Create(input, [header + zulu + alpha]);
        var editor = new DocxDocumentEditor();

        var request = new SortDocumentRequest(
            input, output, SortingProfiles.Get(SortingProfiles.DefaultIdentifier),
            "Nombre y Apellidos");
        await editor.SortAsync(request, CancellationToken.None);

        var snapshot = await new DocxDocumentReader().ReadAsync(
            output, "Nombre y Apellidos", CancellationToken.None);
        Assert.Equal(["Alpha Norte ANA", "Zulu Norte ANA"],
            snapshot.Records.Select(record => record.OriginalName));
    }

    [Fact]
    public async Task SortAsync_PartialHeader_RejectsDocument()
    {
        using var directory = new TemporaryDirectory();
        var input = directory.File("input.docx");
        var output = directory.File("output.docx");
        var partial = DocxTestPackage.Header.Replace(
            "Nombre y Apellidos", "Código - Nombre y Apellidos", StringComparison.Ordinal);
        DocxTestPackage.Create(input, [partial + DocxTestPackage.PersonRow("Prueba Norte ANA", "A")]);
        var editor = new DocxDocumentEditor();

        var request = new SortDocumentRequest(
            input, output, SortingProfiles.Get(SortingProfiles.DefaultIdentifier),
            "Nombre y Apellidos");
        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            editor.SortAsync(request, CancellationToken.None));

        Assert.Contains("cabecera exacta", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTransferPlanAsync_ConflictingStyleAndNumbering_RemapsSourceRow()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.File("source.docx");
        var destinationPath = directory.File("destination.docx");
        var outputPath = directory.File("output.docx");
        DocxTestPackage.Create(sourcePath, [DocxTestPackage.Header
            + DocxTestPackage.PersonRow(
                "Prueba Norte ANA", "ORIGEN", "<w:drawing r:embed=\"rId10\"/>")],
            "SOURCE", source: true);
        DocxTestPackage.Create(destinationPath, [DocxTestPackage.Header
            + DocxTestPackage.PersonRow("Ensayo Sur LUIS", "DESTINO")]);
        var reader = new DocxDocumentReader();
        var source = await reader.ReadAsync(sourcePath, "Nombre y Apellidos", CancellationToken.None);
        var destination = await reader.ReadAsync(destinationPath, "Nombre y Apellidos", CancellationToken.None);
        var entry = new PdfEntry("Prueba Norte ANA", source.Records[0].IdentityKey, 1, 1);
        var plan = new TransferPlanner().Create(
            new DocMerge.Domain.Pdf.PdfReadResult([entry], 1, 1), source, destination);
        var editor = new DocxDocumentEditor();

        var request = new ApplyTransferPlanRequest(
            sourcePath, destinationPath, outputPath, plan, "Nombre y Apellidos");
        await editor.ApplyTransferPlanAsync(request, CancellationToken.None);

        var result = await reader.ReadAsync(outputPath, "Nombre y Apellidos", CancellationToken.None);
        Assert.Equal("Prueba Norte ANA", Assert.Single(result.Records).OriginalName);
        Assert.StartsWith("DO_EstiloPersona", result.Records[0].Dependencies.Styles[0], StringComparison.Ordinal);
        Assert.NotEqual("7", result.Records[0].Dependencies.Numbering[0]);
        Assert.Equal("DESTINATION", System.Text.Encoding.UTF8.GetString(
            DocxTestPackage.ReadParts(outputPath)["custom/marker.txt"]));
        var importedResource = DocxTestPackage.ReadParts(outputPath)
            .Single(part => part.Key.StartsWith("word/media/", StringComparison.Ordinal));
        Assert.Equal("SOURCE-RESOURCE", System.Text.Encoding.UTF8.GetString(importedResource.Value));
    }

    [Fact]
    public async Task ValidateAsync_TransferredAndSortedDocument_AcceptsResult()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.File("source.docx");
        var destinationPath = directory.File("destination.docx");
        var transferredPath = directory.File("transferred.docx");
        var sortedPath = directory.File("sorted.docx");
        DocxTestPackage.Create(sourcePath, [DocxTestPackage.Header
            + DocxTestPackage.PersonRow("Prueba Norte ANA", "ORIGEN")], "SOURCE", source: true);
        DocxTestPackage.Create(destinationPath, [DocxTestPackage.Header
            + DocxTestPackage.PersonRow("Ensayo Sur LUIS", "DESTINO")]);
        var reader = new DocxDocumentReader();
        var source = await reader.ReadAsync(sourcePath, "Nombre y Apellidos", CancellationToken.None);
        var destination = await reader.ReadAsync(destinationPath, "Nombre y Apellidos", CancellationToken.None);
        var entry = new PdfEntry("Prueba Norte ANA", source.Records[0].IdentityKey, 1, 1);
        var plan = new TransferPlanner().Create(
            new DocMerge.Domain.Pdf.PdfReadResult([entry], 1, 1), source, destination);
        var editor = new DocxDocumentEditor();
        var profile = SortingProfiles.Get(SortingProfiles.DefaultIdentifier);
        var applyRequest = new ApplyTransferPlanRequest(
            sourcePath, destinationPath, transferredPath, plan, "Nombre y Apellidos");
        await editor.ApplyTransferPlanAsync(applyRequest, CancellationToken.None);
        var sortRequest = new SortDocumentRequest(
            transferredPath, sortedPath, profile, "Nombre y Apellidos");
        await editor.SortAsync(sortRequest, CancellationToken.None);
        var validator = new DocxTransferValidator(reader);

        var validationRequest = new TransferValidationRequest(
            sortedPath, destinationPath, plan, profile, "Nombre y Apellidos");
        var error = await Record.ExceptionAsync(() =>
            validator.ValidateAsync(validationRequest, CancellationToken.None));

        Assert.Null(error);
    }
}
