using System.IO.Compression;
using System.Xml.Linq;
using DocMerge.Infrastructure.Docx;
using DocMerge.Infrastructure.Files;

namespace DocMerge.Infrastructure.Tests.Files;

public sealed class BundledGenerationTemplateProviderTests
{
    private static readonly XNamespace Word =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public async Task GetAsync_EmbeddedTemplate_MaterializesVerifiedCompatibleCopy()
    {
        string path;
        using (var provider = new BundledGenerationTemplateProvider())
        {
            var bundled = await provider.GetAsync(CancellationToken.None);
            path = bundled.Path;
            var snapshot = await new DocxTemplateReader().ReadAsync(
                path, "Nombre y Apellidos", CancellationToken.None);

            Assert.True(File.Exists(path));
            Assert.True(snapshot.SupportsDocumentHeader);
            Assert.Equal("standard-class", bundled.TemplateId);
            Assert.Equal(snapshot.Sha256, bundled.Sha256);
        }

        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public async Task GetAsync_EmbeddedTemplate_CentersTitleAndTutorOnConsecutiveLines()
    {
        using var provider = new BundledGenerationTemplateProvider();
        var bundled = await provider.GetAsync(CancellationToken.None);
        using var archive = ZipFile.OpenRead(bundled.Path);
        await using var stream = archive.GetEntry("word/document.xml")!.Open();
        var document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, CancellationToken.None);
        var titleParagraph = FindFieldParagraph(document, "DocMerge.ClassTitle");
        var tutorParagraph = FindFieldParagraph(document, "DocMerge.TutorName");

        Assert.Same(tutorParagraph, titleParagraph.ElementsAfterSelf(Word + "p").First());
        Assert.Equal("center", ReadAlignment(titleParagraph));
        Assert.Equal("center", ReadAlignment(tutorParagraph));
    }

    private static XElement FindFieldParagraph(XDocument document, string tag)
    {
        return document.Descendants(Word + "sdt")
            .Single(control => control.Element(Word + "sdtPr")?
                .Element(Word + "tag")?
                .Attribute(Word + "val")?.Value == tag)
            .Parent!;
    }

    private static string? ReadAlignment(XElement paragraph)
    {
        return paragraph.Element(Word + "pPr")?
            .Element(Word + "jc")?
            .Attribute(Word + "val")?.Value;
    }
}
