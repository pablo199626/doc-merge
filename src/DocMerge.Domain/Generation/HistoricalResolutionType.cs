namespace DocMerge.Domain.Generation;

/// <summary>Identifies the result of resolving one PDF student against historical data.</summary>
public enum HistoricalResolutionType
{
    /// <summary>Exactly one historical candidate was found.</summary>
    UniqueMatch,

    /// <summary>Multiple candidates with demonstrably identical information were found.</summary>
    EquivalentMatches,

    /// <summary>No historical candidate was found.</summary>
    MissingHistoricalStudent,

    /// <summary>Multiple candidates contain different or non-demonstrably-equivalent information.</summary>
    ConflictingHistoricalData,

    /// <summary>The PDF assignments do not permit a safe name-only association.</summary>
    AmbiguousStudentIdentity,
}
