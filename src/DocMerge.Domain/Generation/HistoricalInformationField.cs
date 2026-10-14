namespace DocMerge.Domain.Generation;

/// <summary>Represents readable historical information prepared for review.</summary>
/// <param name="Header">The normalized source column header.</param>
/// <param name="Text">The visible text extracted from the cell.</param>
/// <param name="HasNonTextContent">Whether the cell also contains rich content not represented by text.</param>
/// <param name="RichContent">The presentation-neutral rich content extracted from the cell.</param>
public sealed record HistoricalInformationField(
    string Header,
    string Text,
    bool HasNonTextContent,
    HistoricalRichContent? RichContent = null)
{
    /// <summary>Gets the rich content or an empty value when an older producer supplied none.</summary>
    public HistoricalRichContent PreviewContent { get; } = RichContent ?? HistoricalRichContent.Empty;
}
