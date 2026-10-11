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
                HistoricalResolutionType.MissingHistoricalStudent, candidates, null);
        }

        if (candidates.Count == 1)
        {
            return new HistoricalResolution(
                HistoricalResolutionType.UniqueMatch, candidates, candidates[0]);
        }

        if (HaveEquivalentInformation(candidates))
        {
            return new HistoricalResolution(
                HistoricalResolutionType.EquivalentMatches, candidates, candidates[0]);
        }

        return new HistoricalResolution(
            HistoricalResolutionType.ConflictingHistoricalData, candidates, null);
    }

    private static bool HaveEquivalentInformation(
        IReadOnlyList<HistoricalStudentCandidate> candidates)
    {
        var signature = candidates[0].InformationSignature;
        return signature.Length > 0 && candidates.All(candidate => string.Equals(
            candidate.InformationSignature, signature, StringComparison.Ordinal));
    }
}
