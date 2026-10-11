namespace DocMerge.Domain.Generation;

/// <summary>Represents one student assignment extracted from a class PDF.</summary>
/// <param name="OriginalName">The original name from the PDF.</param>
/// <param name="IdentityKey">The certified exact identity key.</param>
/// <param name="MatchKey">The tolerant class-generation match key.</param>
/// <param name="StudentNumber">The student number supplied by the PDF.</param>
/// <param name="PageNumber">The one-based source page number.</param>
/// <param name="Ordinal">The one-based ordinal declared by the PDF.</param>
public sealed record ClassRosterEntry(
    string OriginalName,
    string IdentityKey,
    string MatchKey,
    string StudentNumber,
    int PageNumber,
    int Ordinal)
{
    /// <summary>Gets a human-readable source location.</summary>
    public string Location => $"página {PageNumber}, entrada {Ordinal}";
}
