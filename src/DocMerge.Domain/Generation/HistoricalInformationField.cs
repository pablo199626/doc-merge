namespace DocMerge.Domain.Generation;

/// <summary>Represents readable historical information prepared for review.</summary>
/// <param name="Header">The normalized source column header.</param>
/// <param name="Text">The visible text extracted from the cell.</param>
/// <param name="HasNonTextContent">Whether the cell also contains rich content not represented by text.</param>
public sealed record HistoricalInformationField(
    string Header,
    string Text,
    bool HasNonTextContent);
