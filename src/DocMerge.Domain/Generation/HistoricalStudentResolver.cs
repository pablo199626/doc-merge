namespace DocMerge.Domain.Generation;

/// <summary>Resolves student names conservatively against the complete historical index.</summary>
public sealed class HistoricalStudentResolver
{
    /// <summary>Resolves one tolerant match key.</summary>
    /// <param name="matchKey">The tolerant student-name key.</param>
    /// <param name="index">The complete historical index.</param>
    /// <returns>The conservative resolution and all considered candidates.</returns>
    public HistoricalResolution Resolve(string matchKey, HistoricalStudentIndex index)
    {
        ArgumentNullException.ThrowIfNull(matchKey);
        ArgumentNullException.ThrowIfNull(index);
        var candidates = index.Find(matchKey);
        if (candidates.Count == 0)
        {
            return new HistoricalResolution(
                HistoricalResolutionType.NewStudent, candidates, null);
        }

        if (candidates.Count == 1)
        {
            return new HistoricalResolution(
                HistoricalResolutionType.Found, candidates, candidates[0]);
        }

        return new HistoricalResolution(
            HistoricalResolutionType.Conflict, candidates, null);
    }
}
