using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class ClassGenerationPlannerTests
{
    [Fact]
    public void Create_FoundAndNewStudents_PlansBothRowsWithoutIssues()
    {
        var roster = Roster("A", Entry("11111111", "ALUMNO, UNO"), Entry("22222222", "ALUMNO, DOS"));
        var candidate = Candidate("history.docx", "alumno uno");
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([roster], new HistoricalStudentIndex([candidate]));

        Assert.False(result.HasBlockingIssues);
        Assert.Collection(
            result.Classes[0].Rows,
            row => Assert.Equal(HistoricalResolutionType.Found, row.Resolution.ResolutionType),
            row => Assert.Equal(
                HistoricalResolutionType.NewStudent,
                row.Resolution.ResolutionType));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Create_SameNameWithDifferentStudentNumbers_CreatesConflictBeforeHistoricalResolution()
    {
        var first = Roster("A", Entry("11111111", "BENALI, OMAR"));
        var second = Roster("B", Entry("22222222", "BENALI OMAR"));
        var candidates = new[]
        {
            Candidate("history.docx", "benali omar"),
            Candidate("history-copy.docx", "benali omar"),
        };
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([first, second], new HistoricalStudentIndex(candidates));

        Assert.False(result.HasBlockingIssues);
        Assert.All(
            result.Classes.SelectMany(plan => plan.Rows),
            row => Assert.Equal(
                HistoricalResolutionType.Conflict,
                row.Resolution.ResolutionType));
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "DuplicateStudentName"));
        Assert.All(
            result.Issues.Where(issue => issue.Code == "DuplicateStudentName"),
            issue =>
            {
                Assert.Equal(GenerationIssueSeverity.Warning, issue.Severity);
                Assert.Contains("distintos NIA", issue.Detail, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Create_DuplicateHistoricalRowsInSameDocument_OmitsStudentAndReportsConflict()
    {
        var roster = Roster("A", Entry("11111111", "ALUMNO, UNO"));
        var candidates = new[]
        {
            Candidate("history.docx", "alumno uno", rowIndex: 1),
            Candidate("history.docx", "alumno uno", rowIndex: 2),
        };
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([roster], new HistoricalStudentIndex(candidates));

        var classPlan = Assert.Single(result.Classes);
        Assert.Equal(HistoricalResolutionType.Conflict, Assert.Single(classPlan.Rows).Resolution.ResolutionType);
        Assert.False(classPlan.Rows[0].IsIncluded);
        Assert.Empty(classPlan.IncludedRows);
        Assert.False(classPlan.ProducesDocument);
        Assert.Empty(result.OutputClasses);
        Assert.Equal(1, result.OmittedStudentCount);
        Assert.Contains(result.Issues, issue => issue.Code == "DuplicateStudentName");
    }

    [Fact]
    public void Create_OneEmptyAndOneIncludedClass_PlansOnlyIncludedDocument()
    {
        var conflicted = Roster("A", Entry("11111111", "ALUMNO, UNO"));
        var newStudent = Roster("B", Entry("22222222", "ALUMNO, DOS"));
        var candidates = new[]
        {
            Candidate("first.docx", "alumno uno"),
            Candidate("second.docx", "alumno uno"),
        };
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([conflicted, newStudent], new HistoricalStudentIndex(candidates));

        var output = Assert.Single(result.OutputClasses);
        Assert.Equal("2ESOB", output.Roster.ClassIdentifier.CanonicalCode);
        Assert.Equal(2, result.RosterStudentCount);
        Assert.Equal(1, result.IncludedStudentCount);
        Assert.Equal(1, result.OmittedStudentCount);
    }

    [Fact]
    public void Create_DuplicateHistoryWithoutRosterAssignment_DoesNotReportConflict()
    {
        var roster = Roster("A", Entry("11111111", "ALUMNO, UNO"));
        var candidates = new[]
        {
            Candidate("first.docx", "alumno ausente"),
            Candidate("second.docx", "alumno ausente"),
        };
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());

        var result = planner.Create([roster], new HistoricalStudentIndex(candidates));

        Assert.Empty(result.Issues);
        Assert.Equal(HistoricalResolutionType.NewStudent, Assert.Single(result.Classes[0].Rows).Resolution.ResolutionType);
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
        int rowIndex = 1)
    {
        var record = new DocumentRecord(
            path,
            0,
            rowIndex,
            matchKey,
            matchKey,
            "<row />",
            new RecordDependencies([], [], []));
        return new HistoricalStudentCandidate(record, matchKey);
    }
}
