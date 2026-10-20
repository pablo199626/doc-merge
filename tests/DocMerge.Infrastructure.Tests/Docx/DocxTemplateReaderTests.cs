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

    [Fact]
    public async Task ReadAsync_ValidDocumentHeaderFields_ReportsPersonalizationSupport()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(
            path,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            documentHeaderFields: true);

        var result = await new DocxTemplateReader().ReadAsync(
            path, "Nombre y Apellidos", CancellationToken.None);

        Assert.True(result.SupportsDocumentHeader);
        Assert.Equal(1, result.HeaderContractVersion);
    }

    [Fact]
    public async Task ReadAsync_LegacyTemplate_ReportsNoPersonalizationSupport()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(path, [DocxTestPackage.Header + DocxTestPackage.EmptyRow]);

        var result = await new DocxTemplateReader().ReadAsync(
            path, "Nombre y Apellidos", CancellationToken.None);

        Assert.False(result.SupportsDocumentHeader);
    }

    [Theory]
    [InlineData(true, false, "Falta")]
    [InlineData(false, true, "duplicado")]
    public async Task ReadAsync_InvalidDocumentHeaderFields_ThrowsStructureException(
        bool omitTutor,
        bool duplicateTitle,
        string expectedMessage)
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(
            path,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            documentHeaderFields: true,
            omitTutorField: omitTutor,
            duplicateClassTitleField: duplicateTitle);

        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            new DocxTemplateReader().ReadAsync(
                path, "Nombre y Apellidos", CancellationToken.None));

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_UndeclaredCompatibilityPrefix_ThrowsStructureException()
    {
        using var temporary = new TemporaryDirectory();
        var path = temporary.File("template.docx");
        DocxTestPackage.Create(
            path,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            invalidCompatibilityPrefix: true);

        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            new DocxTemplateReader().ReadAsync(
                path, "Nombre y Apellidos", CancellationToken.None));

        Assert.Contains("w14", error.Message, StringComparison.Ordinal);
        Assert.Contains("sin definir", error.Message, StringComparison.Ordinal);
    }
}
