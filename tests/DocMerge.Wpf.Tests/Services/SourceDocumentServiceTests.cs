using System.IO;
using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.Tests.Services;

public sealed class SourceDocumentServiceTests
{
    [Fact]
    public void Open_RelativePath_RejectsSource()
    {
        var service = new SourceDocumentService();

        var error = Assert.Throws<InvalidOperationException>(() => service.Open("document.pdf"));

        Assert.Contains("absoluta", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_UnsupportedExtension_RejectsSource()
    {
        var service = new SourceDocumentService();
        var path = Path.Combine(Path.GetTempPath(), "document.exe");

        var error = Assert.Throws<InvalidOperationException>(() => service.Open(path));

        Assert.Contains("PDF o DOCX", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_MissingSupportedDocument_ReportsAbsence()
    {
        var service = new SourceDocumentService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

        Assert.Throws<FileNotFoundException>(() => service.Open(path));
    }
}
