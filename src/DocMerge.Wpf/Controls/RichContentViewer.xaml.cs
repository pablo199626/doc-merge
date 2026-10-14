using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DocMerge.Domain.Generation;

namespace DocMerge.Wpf.Controls;

/// <summary>Displays presentation-neutral historical rich content as a read-only WPF document.</summary>
public partial class RichContentViewer : UserControl
{
    /// <summary>Identifies the <see cref="RichContent"/> dependency property.</summary>
    public static readonly DependencyProperty RichContentProperty = DependencyProperty.Register(
        nameof(RichContent),
        typeof(HistoricalRichContent),
        typeof(RichContentViewer),
        new PropertyMetadata(HistoricalRichContent.Empty, OnContentChanged));

    /// <summary>Initializes a new instance of the <see cref="RichContentViewer"/> class.</summary>
    public RichContentViewer()
    {
        InitializeComponent();
        Viewer.Document = RichContentFlowDocumentFactory.Create(HistoricalRichContent.Empty);
    }

    /// <summary>Gets or sets the historical rich content to display.</summary>
    public HistoricalRichContent RichContent
    {
        get => (HistoricalRichContent)GetValue(RichContentProperty);
        set => SetValue(RichContentProperty, value);
    }

    private static void OnContentChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var control = (RichContentViewer)sender;
        var content = args.NewValue as HistoricalRichContent ?? HistoricalRichContent.Empty;
        control.Viewer.Document = RichContentFlowDocumentFactory.Create(content);
    }
}

internal static class RichContentFlowDocumentFactory
{
    private const double PointsToDip = 96d / 72d;

    internal static FlowDocument Create(HistoricalRichContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = Brushes.Black,
            Background = Brushes.Transparent,
        };
        foreach (var source in content.Paragraphs)
        {
            document.Blocks.Add(CreateParagraph(source));
        }

        return document;
    }

    private static Paragraph CreateParagraph(HistoricalRichParagraph source)
    {
        var formatting = source.Formatting;
        var paragraph = new Paragraph
        {
            TextAlignment = formatting.Alignment switch
            {
                HistoricalParagraphAlignment.Center => TextAlignment.Center,
                HistoricalParagraphAlignment.Right => TextAlignment.Right,
                HistoricalParagraphAlignment.Justify => TextAlignment.Justify,
                _ => TextAlignment.Left,
            },
            Margin = new Thickness(
                formatting.LeftIndentPoints * PointsToDip,
                formatting.SpaceBeforePoints * PointsToDip,
                formatting.RightIndentPoints * PointsToDip,
                formatting.SpaceAfterPoints * PointsToDip),
            TextIndent = formatting.FirstLineIndentPoints * PointsToDip,
        };
        if (source.List is not null)
        {
            paragraph.Inlines.Add(new Run(ListMarker(source.List)) { FontWeight = FontWeights.SemiBold });
            paragraph.Margin = new Thickness(
                paragraph.Margin.Left + ((source.List.Level + 1) * 18),
                paragraph.Margin.Top,
                paragraph.Margin.Right,
                paragraph.Margin.Bottom);
        }

        foreach (var inline in source.Inlines)
        {
            paragraph.Inlines.Add(CreateInline(inline));
        }

        return paragraph;
    }

    private static Inline CreateInline(HistoricalRichInline source) => source.Kind switch
    {
        HistoricalRichInlineKind.LineBreak => new LineBreak(),
        HistoricalRichInlineKind.Tab => new Run("\t"),
        HistoricalRichInlineKind.UnsupportedContent => new Run(source.Text)
        {
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.DimGray,
        },
        _ => CreateTextRun(source),
    };

    private static Run CreateTextRun(HistoricalRichInline source)
    {
        var formatting = source.Formatting;
        var run = new Run(source.Text)
        {
            FontWeight = formatting.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = formatting.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = formatting.IsUnderlined ? TextDecorations.Underline : null,
        };
        if (!string.IsNullOrWhiteSpace(formatting.FontFamily))
        {
            run.FontFamily = new FontFamily(formatting.FontFamily);
        }

        if (formatting.FontSizePoints is > 0 and < 200)
        {
            run.FontSize = formatting.FontSizePoints.Value * PointsToDip;
        }

        if (TryBrush(formatting.ForegroundColor, out var foreground))
        {
            run.Foreground = foreground;
        }

        if (TryBrush(formatting.HighlightColor, out var background))
        {
            run.Background = background;
        }

        return run;
    }

    private static string ListMarker(HistoricalListDescriptor list)
    {
        if (list.Format == "bullet")
        {
            return $"{list.LevelText} ";
        }

        return $"{list.LevelText.Replace("%1", "1", StringComparison.Ordinal)} ";
    }

    private static bool TryBrush(string? value, out Brush brush)
    {
        brush = Brushes.Transparent;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var colorValue = HighlightColor(value) ?? $"#{value}";
        try
        {
            brush = (Brush)new BrushConverter().ConvertFromString(
                context: null,
                CultureInfo.InvariantCulture,
                colorValue)!;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static string? HighlightColor(string value) => value.ToLowerInvariant() switch
    {
        "black" => "#000000",
        "blue" => "#0000FF",
        "cyan" => "#00FFFF",
        "green" => "#00FF00",
        "magenta" => "#FF00FF",
        "red" => "#FF0000",
        "yellow" => "#FFFF00",
        "white" => "#FFFFFF",
        "darkblue" => "#000080",
        "darkcyan" => "#008080",
        "darkgreen" => "#008000",
        "darkmagenta" => "#800080",
        "darkred" => "#800000",
        "darkyellow" => "#808000",
        "darkgray" => "#808080",
        "lightgray" => "#C0C0C0",
        _ => null,
    };
}
