namespace DocMerge.Domain.Transfer;

/// <summary>Specifies a condition that requires review or prevents a safe transfer.</summary>
public enum IssueType
{
    /// <summary>The PDF contains the same normalized identity more than once.</summary>
    DuplicatePdfEntry,
    /// <summary>The destination contains multiple rows for the identity.</summary>
    AmbiguousDestination,
    /// <summary>The identity is absent from the source document.</summary>
    MissingSource,
    /// <summary>The source contains multiple rows for the identity.</summary>
    AmbiguousSource,
}
