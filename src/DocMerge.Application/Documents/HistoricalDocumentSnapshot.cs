using DocMerge.Domain.Generation;

namespace DocMerge.Application.Documents;

/// <summary>Represents one validated historical DOCX and its indexed candidates.</summary>
public sealed record HistoricalDocumentSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="HistoricalDocumentSnapshot"/> class.</summary>
    /// <param name="documentPath">The historical document path.</param>
    /// <param name="sha256">The lowercase SHA-256 hash of the document.</param>
    /// <param name="columnHeaders">The normalized visible column headers.</param>
    /// <param name="candidates">The historical student candidates.</param>
    public HistoricalDocumentSnapshot(
        string documentPath,
        string sha256,
        IReadOnlyList<string> columnHeaders,
        IReadOnlyList<HistoricalStudentCandidate> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(columnHeaders);
        ArgumentNullException.ThrowIfNull(candidates);
        DocumentPath = documentPath;
        Sha256 = sha256;
        ColumnHeaders = Array.AsReadOnly(columnHeaders.ToArray());
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    /// <summary>Gets the historical document path.</summary>
    public string DocumentPath { get; }

    /// <summary>Gets the lowercase SHA-256 hash of the document.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the normalized visible column headers.</summary>
    public IReadOnlyList<string> ColumnHeaders { get; }

    /// <summary>Gets the historical student candidates.</summary>
    public IReadOnlyList<HistoricalStudentCandidate> Candidates { get; }
}
