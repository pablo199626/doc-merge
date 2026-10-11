namespace DocMerge.Application.Files;

/// <summary>Contains deterministic file paths discovered for class generation.</summary>
/// <param name="HistoricalDocumentPaths">The historical DOCX paths.</param>
/// <param name="RosterPdfPaths">The class PDF paths.</param>
public sealed record GenerationInputSet(
    IReadOnlyList<string> HistoricalDocumentPaths,
    IReadOnlyList<string> RosterPdfPaths);
