using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class HistoricalRichContentTests
{
    [Fact]
    public void Constructor_MutableInputs_CopiesCollections()
    {
        var inlines = new List<HistoricalRichInline>
        {
            new(HistoricalRichInlineKind.Text, "Inicial", HistoricalTextFormatting.Default),
        };
        var paragraphs = new List<HistoricalRichParagraph>
        {
            new(inlines, HistoricalParagraphFormatting.Default),
        };

        var content = new HistoricalRichContent(paragraphs);
        inlines.Add(new HistoricalRichInline(
            HistoricalRichInlineKind.Text,
            "Posterior",
            HistoricalTextFormatting.Default));
        paragraphs.Clear();

        var paragraph = Assert.Single(content.Paragraphs);
        Assert.Single(paragraph.Inlines);
        Assert.Equal("Inicial", paragraph.Inlines[0].Text);
    }
}
