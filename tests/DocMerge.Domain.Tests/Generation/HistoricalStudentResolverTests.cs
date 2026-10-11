using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class HistoricalStudentResolverTests
{
    [Fact]
    public void Resolve_NoCandidate_ReturnsMissingResolution()
    {
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.MissingHistoricalStudent, result.ResolutionType);
        Assert.Null(result.SelectedCandidate);
    }

    [Fact]
    public void Resolve_OneCandidate_ReturnsUniqueMatch()
    {
        var candidate = Candidate("one.docx", "alumno uno", "A");
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([candidate]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.UniqueMatch, result.ResolutionType);
        Assert.Same(candidate, result.SelectedCandidate);
    }

    [Fact]
    public void Resolve_EquivalentCandidates_ReturnsFirstDeterministicCandidate()
    {
        var first = Candidate("first.docx", "alumno uno", "SAME");
        var second = Candidate("second.docx", "alumno uno", "SAME");
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([first, second]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.EquivalentMatches, result.ResolutionType);
        Assert.Same(first, result.SelectedCandidate);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_DifferentCandidates_ReturnsBlockingConflictWithoutSelection()
    {
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex(
            [Candidate("first.docx", "alumno uno", "A"), Candidate("second.docx", "alumno uno", "B")]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.ConflictingHistoricalData, result.ResolutionType);
        Assert.Null(result.SelectedCandidate);
    }

    private static HistoricalStudentCandidate Candidate(
        string path,
        string matchKey,
        string signature)
    {
        var record = new DocumentRecord(
            path,
            0,
            1,
            matchKey,
            matchKey,
            "<row />",
            new RecordDependencies([], [], []));
        return new HistoricalStudentCandidate(record, matchKey, signature);
    }
}
