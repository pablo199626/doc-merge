using DocMerge.Application.Documents;
using DocMerge.Infrastructure.Docx;

namespace DocMerge.Infrastructure.Tests.Docx;

public sealed class DocxTemplateReaderTests
{
    [Fact]
    public async Task ReadAsync_EmptyPrototype_ReturnsTemplateStructure()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(path, [DocxTestPackage.Header + DocxTestPackage.EmptyRow]);
        var reader = new DocxTemplateReader();

        var result = await reader.ReadAsync(path, "Nombre y Apellidos", CancellationToken.None);

        Assert.Equal(1, result.NameColumnIndex);
        Assert.Equal([string.Empty, "Nombre y Apellidos", "Medidas"], result.ColumnHeaders);
        Assert.Contains("<w:tr", result.PrototypeRowXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MissingPrototype_ThrowsStructureException()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(path, [DocxTestPackage.Header]);
        var reader = new DocxTemplateReader();

        var error = await Assert.ThrowsAsync<DocumentStructureException>(
            () => reader.ReadAsync(path, "Nombre y Apellidos", CancellationToken.None));

        Assert.Contains("exclusivamente una fila vacía", error.Message, StringComparison.Ordinal);
    }
}
