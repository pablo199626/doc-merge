namespace DocMerge.Domain.Generation;

/// <summary>Describes the conservative resolution of one PDF student.</summary>
public sealed record HistoricalResolution
{
    /// <summary>Initializes a new instance of the <see cref="HistoricalResolution"/> class.</summary>
    /// <param name="resolutionType">The resolution classification.</param>
    /// <param name="candidates">All historical candidates considered.</param>
    /// <param name="selectedCandidate">The representative candidate when the resolution is safe.</param>
    public HistoricalResolution(
        HistoricalResolutionType resolutionType,
        IReadOnlyList<HistoricalStudentCandidate> candidates,
        HistoricalStudentCandidate? selectedCandidate)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Validate(resolutionType, Candidates, selectedCandidate);
        ResolutionType = resolutionType;
        SelectedCandidate = selectedCandidate;
    }

    /// <summary>Gets the resolution classification.</summary>
    public HistoricalResolutionType ResolutionType { get; }

    /// <summary>Gets all historical candidates considered.</summary>
    public IReadOnlyList<HistoricalStudentCandidate> Candidates { get; }

    /// <summary>Gets the representative candidate when the resolution is safe.</summary>
    public HistoricalStudentCandidate? SelectedCandidate { get; }

    private static void Validate(
        HistoricalResolutionType resolutionType,
        IReadOnlyList<HistoricalStudentCandidate> candidates,
        HistoricalStudentCandidate? selectedCandidate)
    {
        var isValid = resolutionType switch
        {
            HistoricalResolutionType.Found => candidates.Count == 1
                && ReferenceEquals(candidates[0], selectedCandidate),
            HistoricalResolutionType.NewStudent => candidates.Count == 0 && selectedCandidate is null,
            HistoricalResolutionType.Conflict => selectedCandidate is null,
            _ => false,
        };
        if (!isValid)
        {
            throw new ArgumentException("La resolución histórica no cumple las invariantes de su estado.");
        }
    }
}
