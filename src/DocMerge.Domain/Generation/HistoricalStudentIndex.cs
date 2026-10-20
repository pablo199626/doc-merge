using System.Collections.ObjectModel;

namespace DocMerge.Domain.Generation;

/// <summary>Indexes every historical candidate without discarding duplicate matches.</summary>
public sealed class HistoricalStudentIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<HistoricalStudentCandidate>> _candidates;

    /// <summary>Initializes a new instance of the <see cref="HistoricalStudentIndex"/> class.</summary>
    /// <param name="candidates">The historical candidates in deterministic discovery order.</param>
    public HistoricalStudentIndex(IReadOnlyList<HistoricalStudentCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var groups = candidates.GroupBy(candidate => candidate.MatchKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<HistoricalStudentCandidate>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        _candidates = new ReadOnlyDictionary<string, IReadOnlyList<HistoricalStudentCandidate>>(groups);
        Count = candidates.Count;
    }

    /// <summary>Gets the number of indexed historical records.</summary>
    public int Count { get; }

    /// <summary>Finds all candidates with the requested tolerant match key.</summary>
    /// <param name="matchKey">The tolerant match key.</param>
    /// <returns>All matching candidates in deterministic discovery order.</returns>
    public IReadOnlyList<HistoricalStudentCandidate> Find(string matchKey)
    {
        ArgumentNullException.ThrowIfNull(matchKey);
        return _candidates.GetValueOrDefault(matchKey) ?? [];
    }
}
