namespace DocMerge.Application.Generation;

/// <summary>Identifies the inputs required to prepare a class-generation batch.</summary>
/// <param name="HistoricalDirectory">The historical DOCX root directory.</param>
/// <param name="RosterDirectory">The class PDF root directory.</param>
/// <param name="TemplatePath">The empty DOCX template path.</param>
/// <param name="Header">The exact student-name table header.</param>
public sealed record ClassGenerationRequest(
    string HistoricalDirectory,
    string RosterDirectory,
    string TemplatePath,
    string Header);
