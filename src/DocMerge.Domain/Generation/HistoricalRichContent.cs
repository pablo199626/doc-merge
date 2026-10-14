namespace DocMerge.Domain.Generation;

/// <summary>Represents presentation-neutral rich content extracted from a historical document.</summary>
public sealed record HistoricalRichContent
{
    /// <summary>Initializes a new instance of the <see cref="HistoricalRichContent"/> class.</summary>
    /// <param name="paragraphs">The paragraphs in source order.</param>
    public HistoricalRichContent(IReadOnlyList<HistoricalRichParagraph> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        Paragraphs = Array.AsReadOnly(paragraphs.ToArray());
    }

    /// <summary>Gets an empty rich-content value.</summary>
    public static HistoricalRichContent Empty { get; } = new([]);

    /// <summary>Gets the paragraphs in source order.</summary>
    public IReadOnlyList<HistoricalRichParagraph> Paragraphs { get; }

    /// <summary>Gets a value that indicates whether the content has visible elements.</summary>
    public bool HasContent => Paragraphs.Any(paragraph => paragraph.Inlines.Count > 0);
}

/// <summary>Represents one paragraph of historical rich content.</summary>
public sealed record HistoricalRichParagraph
{
    /// <summary>Initializes a new instance of the <see cref="HistoricalRichParagraph"/> class.</summary>
    /// <param name="inlines">The inline elements in source order.</param>
    /// <param name="formatting">The effective paragraph formatting.</param>
    /// <param name="list">The optional list descriptor.</param>
    public HistoricalRichParagraph(
        IReadOnlyList<HistoricalRichInline> inlines,
        HistoricalParagraphFormatting formatting,
        HistoricalListDescriptor? list = null)
    {
        ArgumentNullException.ThrowIfNull(inlines);
        ArgumentNullException.ThrowIfNull(formatting);
        Inlines = Array.AsReadOnly(inlines.ToArray());
        Formatting = formatting;
        List = list;
    }

    /// <summary>Gets the inline elements in source order.</summary>
    public IReadOnlyList<HistoricalRichInline> Inlines { get; }

    /// <summary>Gets the effective paragraph formatting.</summary>
    public HistoricalParagraphFormatting Formatting { get; }

    /// <summary>Gets the list descriptor, when the paragraph belongs to a list.</summary>
    public HistoricalListDescriptor? List { get; }
}

/// <summary>Represents one inline element in historical rich content.</summary>
/// <param name="Kind">The kind of inline element.</param>
/// <param name="Text">The visible text or accessible fallback.</param>
/// <param name="Formatting">The effective character formatting.</param>
public sealed record HistoricalRichInline(
    HistoricalRichInlineKind Kind,
    string Text,
    HistoricalTextFormatting Formatting);

/// <summary>Identifies a supported historical inline element.</summary>
public enum HistoricalRichInlineKind
{
    /// <summary>Visible text.</summary>
    Text,

    /// <summary>A line break.</summary>
    LineBreak,

    /// <summary>A tabulation.</summary>
    Tab,

    /// <summary>Content that cannot be represented by the preview.</summary>
    UnsupportedContent,
}

/// <summary>Represents effective character formatting independently of a UI framework.</summary>
/// <param name="IsBold">Whether the text is bold.</param>
/// <param name="IsItalic">Whether the text is italic.</param>
/// <param name="IsUnderlined">Whether the text is underlined.</param>
/// <param name="FontFamily">The preferred font family, when specified.</param>
/// <param name="FontSizePoints">The font size in points, when specified.</param>
/// <param name="ForegroundColor">The six-digit RGB foreground color, when specified.</param>
/// <param name="HighlightColor">The Word highlight name or RGB color, when specified.</param>
public sealed record HistoricalTextFormatting(
    bool IsBold = false,
    bool IsItalic = false,
    bool IsUnderlined = false,
    string? FontFamily = null,
    double? FontSizePoints = null,
    string? ForegroundColor = null,
    string? HighlightColor = null)
{
    /// <summary>Gets formatting with default values.</summary>
    public static HistoricalTextFormatting Default { get; } = new();
}

/// <summary>Represents effective paragraph formatting independently of a UI framework.</summary>
/// <param name="Alignment">The paragraph alignment.</param>
/// <param name="LeftIndentPoints">The left indentation in points.</param>
/// <param name="RightIndentPoints">The right indentation in points.</param>
/// <param name="FirstLineIndentPoints">The first-line indentation in points.</param>
/// <param name="SpaceBeforePoints">The spacing before the paragraph in points.</param>
/// <param name="SpaceAfterPoints">The spacing after the paragraph in points.</param>
public sealed record HistoricalParagraphFormatting(
    HistoricalParagraphAlignment Alignment = HistoricalParagraphAlignment.Left,
    double LeftIndentPoints = 0,
    double RightIndentPoints = 0,
    double FirstLineIndentPoints = 0,
    double SpaceBeforePoints = 0,
    double SpaceAfterPoints = 0)
{
    /// <summary>Gets formatting with default values.</summary>
    public static HistoricalParagraphFormatting Default { get; } = new();
}

/// <summary>Identifies paragraph alignment.</summary>
public enum HistoricalParagraphAlignment
{
    /// <summary>Left-aligned text.</summary>
    Left,

    /// <summary>Centered text.</summary>
    Center,

    /// <summary>Right-aligned text.</summary>
    Right,

    /// <summary>Justified text.</summary>
    Justify,
}

/// <summary>Describes the source list marker associated with a paragraph.</summary>
/// <param name="Level">The zero-based list level.</param>
/// <param name="Format">The Word numbering format.</param>
/// <param name="LevelText">The source marker pattern.</param>
public sealed record HistoricalListDescriptor(int Level, string Format, string LevelText);
