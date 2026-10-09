using DocMerge.Application.Documents;
using DocMerge.Infrastructure.Docx;

namespace DocMerge.Infrastructure.Tests.Docx;

public sealed class DocxDocumentReaderTests
{
    [Fact]
    public async Task ReadAsync_CompleteRows_ReturnsDependenciesAndAuxiliaryRows()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("people.docx");
        var empty = "<w:tr><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>";
        DocxTestPackage.Create(path, [
            DocxTestPackage.Header + DocxTestPackage.PersonRow("Prueba Norte ANA", "MEDIDA") + empty,
        ]);
        var reader = new DocxDocumentReader();

        var result = await reader.ReadAsync(path, "Nombre y Apellidos", CancellationToken.None);

        Assert.Single(result.Records);
        Assert.Single(result.AuxiliaryRows);
        Assert.Equal("Prueba Norte ANA", result.Records[0].OriginalName);
        Assert.Equal(["EstiloPersona"], result.Records[0].Dependencies.Styles);
        Assert.Equal(["7"], result.Records[0].Dependencies.Numbering);
        Assert.Equal("rId9", result.Records[0].Dependencies.Relationships[0].RelationshipId);
    }

    [Fact]
    public async Task ReadAsync_MultipleExactHeaders_RejectsDocument()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("ambiguous.docx");
        DocxTestPackage.Create(path, [
            DocxTestPackage.Header + DocxTestPackage.PersonRow("Prueba Norte ANA", "A"),
            DocxTestPackage.Header + DocxTestPackage.PersonRow("Ensayo Sur LUIS", "B"),
        ]);
        var reader = new DocxDocumentReader();

        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            reader.ReadAsync(path, "Nombre y Apellidos", CancellationToken.None));

        Assert.Contains("ambigua", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MergedCell_RejectsDocument()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("merged.docx");
        var row = DocxTestPackage.PersonRow(
            "Prueba Norte ANA", "A", "<w:trPr><w:vMerge/></w:trPr>");
        DocxTestPackage.Create(path, [DocxTestPackage.Header + row]);
        var reader = new DocxDocumentReader();

        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            reader.ReadAsync(path, "Nombre y Apellidos", CancellationToken.None));

        Assert.Contains("combinadas verticalmente", error.Message, StringComparison.Ordinal);
    }
}
