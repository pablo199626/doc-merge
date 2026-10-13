namespace DocMerge.Domain.Generation;

/// <summary>Describes the row that must be generated for one PDF assignment.</summary>
/// <param name="RosterEntry">The authoritative PDF assignment.</param>
/// <param name="Resolution">The historical information resolution.</param>
public sealed record StudentRowPlan(
    ClassRosterEntry RosterEntry,
    HistoricalResolution Resolution)
{
    /// <summary>Gets whether this assignment must be written to the generated document.</summary>
    public bool IsIncluded => Resolution.ResolutionType != HistoricalResolutionType.Conflict;
}
