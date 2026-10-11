using DocMerge.Domain.Documents;

namespace DocMerge.Domain.Generation;

/// <summary>Represents one traceable student record from the historical collection.</summary>
/// <param name="Record">The complete historical DOCX record.</param>
/// <param name="MatchKey">The tolerant student-name match key.</param>
/// <param name="InformationSignature">The conservative signature of non-identity information and formatting.</param>
/// <param name="InformationFields">The readable non-identity fields prepared for review.</param>
public sealed record HistoricalStudentCandidate(
    DocumentRecord Record,
    string MatchKey,
    string InformationSignature,
    IReadOnlyList<HistoricalInformationField>? InformationFields = null)
{
    /// <summary>Gets the immutable fields that may be displayed during review.</summary>
    public IReadOnlyList<HistoricalInformationField> ReviewFields { get; } =
        Array.AsReadOnly((InformationFields ?? []).ToArray());
}
