using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class HistoricalStudentResolverTests
{
    [Fact]
    public void Resolve_NoCandidate_ReturnsNewStudent()
    {
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.NewStudent, result.ResolutionType);
        Assert.Null(result.SelectedCandidate);
    }

    [Fact]
    public void Resolve_OneCandidate_ReturnsFound()
    {
        var candidate = Candidate("one.docx", "alumno uno");
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([candidate]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.Found, result.ResolutionType);
        Assert.Same(candidate, result.SelectedCandidate);
    }

    [Fact]
    public void Resolve_EquivalentCandidates_ReturnsConflictWithoutSelection()
    {
        var first = Candidate("first.docx", "alumno uno");
        var second = Candidate("second.docx", "alumno uno");
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex([first, second]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.Conflict, result.ResolutionType);
        Assert.Null(result.SelectedCandidate);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_DifferentCandidates_ReturnsConflictWithoutSelection()
    {
        var resolver = new HistoricalStudentResolver();
        var index = new HistoricalStudentIndex(
            [Candidate("first.docx", "alumno uno"), Candidate("second.docx", "alumno uno")]);

        var result = resolver.Resolve("alumno uno", index);

        Assert.Equal(HistoricalResolutionType.Conflict, result.ResolutionType);
        Assert.Null(result.SelectedCandidate);
    }

    private static HistoricalStudentCandidate Candidate(
        string path,
        string matchKey)
    {
        var record = new DocumentRecord(
            path,
            0,
            1,
            matchKey,
            matchKey,
            "<row />",
            new RecordDependencies([], [], []));
        return new HistoricalStudentCandidate(record, matchKey);
    }
}
