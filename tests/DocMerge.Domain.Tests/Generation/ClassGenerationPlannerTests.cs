using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class ClassGenerationPlannerTests
{
    [Fact]
    public void Create_UniqueAndMissingStudents_PlansBothRowsAndReportsMissingStudent()
    {
        var roster = Roster("A", Entry("11111111", "ALUMNO, UNO"), Entry("22222222", "ALUMNO, DOS"));
        var candidate = Candidate("history.docx", "alumno uno", "DATA");
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([roster], new HistoricalStudentIndex([candidate]));

        Assert.False(result.HasBlockingIssues);
        Assert.Collection(
            result.Classes[0].Rows,
            row => Assert.Equal(HistoricalResolutionType.UniqueMatch, row.Resolution.ResolutionType),
            row => Assert.Equal(
                HistoricalResolutionType.MissingHistoricalStudent,
                row.Resolution.ResolutionType));
        Assert.Contains(result.Issues, issue => issue.Code == "MissingHistoricalStudent");
        Assert.Contains(result.Issues, issue => issue.Code == "PdfWithoutDeclaredTotal");
    }

    [Fact]
    public void Create_SameNameWithDifferentStudentNumbers_BlocksHistoricalAssociation()
    {
        var first = Roster("A", Entry("11111111", "BENALI, OMAR"));
        var second = Roster("B", Entry("22222222", "BENALI OMAR"));
        var candidate = Candidate("history.docx", "benali omar", "DATA");
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([first, second], new HistoricalStudentIndex([candidate]));

        Assert.False(result.HasBlockingIssues);
        Assert.All(
            result.Classes.SelectMany(plan => plan.Rows),
            row => Assert.Equal(
                HistoricalResolutionType.AmbiguousStudentIdentity,
                row.Resolution.ResolutionType));
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "AmbiguousStudentIdentity"));
        Assert.All(
            result.Issues.Where(issue => issue.Code == "AmbiguousStudentIdentity"),
            issue => Assert.Equal(GenerationIssueSeverity.Warning, issue.Severity));
    }

    [Fact]
    public void Create_SameStudentNumberInDifferentClasses_ReportsBlockingIssue()
    {
        var first = Roster("A", Entry("11111111", "ALUMNO, UNO"));
        var second = Roster("B", Entry("11111111", "ALUMNO, UNO"));
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([first, second], new HistoricalStudentIndex([]));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "DuplicateStudentNumber"
            && issue.Severity == GenerationIssueSeverity.Blocking);
        var issue = Assert.Single(result.Issues, item => item.Code == "DuplicateStudentNumber");
        Assert.Collection(
            issue.SourceReferences,
            reference =>
            {
                Assert.Equal("11111111", reference.StudentNumber);
                Assert.Equal("2ESOA", reference.ClassIdentifier!.CanonicalCode);
            },
            reference =>
            {
                Assert.Equal("11111111", reference.StudentNumber);
                Assert.Equal("2ESOB", reference.ClassIdentifier!.CanonicalCode);
            });
    }

    private static ClassRoster Roster(string group, params ClassRosterEntry[] entries)
    {
        return new ClassRoster(
            $"2ESO{group}.pdf",
            group,
            new ClassIdentifier("ESO", 2, group),
            entries,
            1,
            hasDeclaredTotal: false);
    }

    private static ClassRosterEntry Entry(string studentNumber, string name)
    {
        return new ClassRosterEntry(
            name,
            name,
            StudentNameMatchKey.Create(name),
            studentNumber,
            1,
            1);
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
