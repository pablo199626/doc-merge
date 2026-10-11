using DocMerge.Domain.Documents;

namespace DocMerge.Domain.Generation;

/// <summary>Represents one traceable student record from the historical collection.</summary>
/// <param name="Record">The complete historical DOCX record.</param>
/// <param name="MatchKey">The tolerant student-name match key.</param>
/// <param name="InformationSignature">The conservative signature of non-identity information and formatting.</param>
public sealed record HistoricalStudentCandidate(
    DocumentRecord Record,
    string MatchKey,
    string InformationSignature);
