namespace DocMerge.Domain.Generation;

/// <summary>Identifies the result of resolving one PDF student against historical data.</summary>
public enum HistoricalResolutionType
{
    /// <summary>Exactly one historical candidate was found.</summary>
    Found,

    /// <summary>No historical candidate was found, so the student is new.</summary>
    NewStudent,

    /// <summary>Historical or PDF data does not permit a unique name-based association.</summary>
    Conflict,
}
