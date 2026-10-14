using DocMerge.Domain.Generation;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class ReviewModelsTests
{
    [Fact]
    public void ClassReview_MixedStates_ReportsIncludedAndOmittedAssignments()
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "A");
        var firstRow = NewStudentRow("ALUMNO, UNO", "12345678", ordinal: 1);
        var secondRow = ConflictRow("ALUMNO, UNO REPETIDO", "87654321", ordinal: 2);
        var roster = new ClassRoster(
            "2ESOA.pdf",
            "hash",
            classIdentifier,
            [firstRow.RosterEntry, secondRow.RosterEntry],
            1,
            hasDeclaredTotal: false);
        var conflictIssue = Issue(
            "DuplicateStudentName",
            classIdentifier,
            "87654321",
            studentName: "ALUMNO, UNO REPETIDO");
        var issues = new[] { conflictIssue };
        var students = new[]
        {
            new StudentReviewViewModel(classIdentifier, firstRow, []),
            new StudentReviewViewModel(classIdentifier, secondRow, [conflictIssue]),
        };
        var review = new ClassReviewViewModel(new ClassDocumentPlan(roster, [firstRow, secondRow]), students, issues);

        Assert.Equal(2, review.StudentCount);
        Assert.Equal(1, review.NewStudentCount);
        Assert.Equal(1, review.ConflictCount);
        Assert.Equal(1, review.IncludedCount);
        Assert.Equal("2 alumnos en el listado · 1 se incluirá · 1 omitido por conflicto", review.OutputSummary);
    }

    [Fact]
    public void StudentReview_NewStudent_ExposesStructuredIdentityAndStatus()
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "C");
        var row = NewStudentRow("ALUMNO, NUEVO", "87654321", ordinal: 1);
        var student = new StudentReviewViewModel(classIdentifier, row, []);

        Assert.Equal("2ESOC", student.ClassIdentifier.CanonicalCode);
        Assert.Equal("87654321", student.StudentNumber);
        Assert.Equal("Alumno nuevo", student.Status);
    }

    private static StudentRowPlan NewStudentRow(string name, string studentNumber, int ordinal)
    {
        var entry = new ClassRosterEntry(
            name,
            name,
            StudentNameMatchKey.Create(name),
            studentNumber,
            1,
            ordinal);
        return new StudentRowPlan(
            entry,
            new HistoricalResolution(HistoricalResolutionType.NewStudent, [], selectedCandidate: null));
    }

    private static StudentRowPlan ConflictRow(string name, string studentNumber, int ordinal)
    {
        var entry = new ClassRosterEntry(
            name,
            name,
            StudentNameMatchKey.Create(name),
            studentNumber,
            1,
            ordinal);
        return new StudentRowPlan(
            entry,
            new HistoricalResolution(HistoricalResolutionType.Conflict, [], selectedCandidate: null));
    }

    private static ReviewIssueViewModel Issue(
        string code,
        ClassIdentifier classIdentifier,
        string? studentNumber,
        string? studentName)
    {
        var issue = new GenerationIssue(
            code,
            GenerationIssueSeverity.Warning,
            classIdentifier,
            studentName,
            "Detalle ficticio.",
            ["2ESOA.pdf"],
            [new GenerationIssueReference("2ESOA.pdf", "documento ficticio", classIdentifier, studentNumber)]);
        return new ReviewIssueViewModel(issue, code, classIdentifier.CanonicalCode, "Consecuencia ficticia.", false);
    }
}
