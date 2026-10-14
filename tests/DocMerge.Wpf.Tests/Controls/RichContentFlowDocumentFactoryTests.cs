using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.Controls;

namespace DocMerge.Wpf.Tests.Controls;

public sealed class RichContentFlowDocumentFactoryTests
{
    [Fact]
    public void Create_FormattedContent_BuildsReadOnlyDocumentElements()
    {
        RunInSta(() =>
        {
            var content = new HistoricalRichContent(
            [
                new HistoricalRichParagraph(
                [
                    new HistoricalRichInline(
                        HistoricalRichInlineKind.Text,
                        "Medida",
                        new HistoricalTextFormatting(
                            IsBold: true,
                            IsItalic: true,
                            IsUnderlined: true,
                            FontFamily: "Arial",
                            FontSizePoints: 10,
                            ForegroundColor: "123456",
                            HighlightColor: "yellow")),
                    new HistoricalRichInline(
                        HistoricalRichInlineKind.LineBreak,
                        string.Empty,
                        HistoricalTextFormatting.Default),
                ],
                new HistoricalParagraphFormatting(HistoricalParagraphAlignment.Center),
                new HistoricalListDescriptor(0, "bullet", "•")),
            ]);

            var document = RichContentFlowDocumentFactory.Create(content);

            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
            Assert.Equal(TextAlignment.Center, paragraph.TextAlignment);
            var runs = paragraph.Inlines.OfType<Run>().ToArray();
            Assert.Equal(2, runs.Length);
            var text = runs[1];
            Assert.Equal("Medida", text.Text);
            Assert.Equal(FontWeights.Bold, text.FontWeight);
            Assert.Equal(FontStyles.Italic, text.FontStyle);
            Assert.Equal("Arial", text.FontFamily.Source);
            Assert.NotNull(text.TextDecorations);
            Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), ((SolidColorBrush)text.Foreground).Color);
            Assert.DoesNotContain(paragraph.Inlines, inline => inline is Hyperlink);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
