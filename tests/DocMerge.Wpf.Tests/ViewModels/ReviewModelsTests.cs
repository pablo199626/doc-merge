using DocMerge.Domain.Generation;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class ReviewModelsTests
{
    [Fact]
    public void ClassReview_DuplicateAssignmentsAndIssues_CountsAffectedStudentOnce()
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "A");
        var firstRow = MissingRow("ALUMNO, UNO", "12345678", ordinal: 1);
        var secondRow = MissingRow("ALUMNO, UNO REPETIDO", "12345678", ordinal: 2);
        var roster = new ClassRoster(
            "2ESOA.pdf",
            "hash",
            classIdentifier,
            [firstRow.RosterEntry, secondRow.RosterEntry],
            1,
            hasDeclaredTotal: false);
        var studentIssue = Issue(
            "MissingHistoricalStudent",
            classIdentifier,
            "12345678",
            studentName: "ALUMNO, UNO");
        var repeatedStudentIssue = Issue(
            "MissingHistoricalStudent",
            classIdentifier,
            "12345678",
            studentName: "ALUMNO, UNO REPETIDO");
        var documentIssue = Issue(
            "PdfWithoutDeclaredTotal",
            classIdentifier,
            studentNumber: null,
            studentName: null);
        var issues = new[] { studentIssue, repeatedStudentIssue, documentIssue };
        var students = new[]
        {
            new StudentReviewViewModel(classIdentifier, firstRow, [studentIssue, repeatedStudentIssue]),
            new StudentReviewViewModel(classIdentifier, secondRow, [studentIssue, repeatedStudentIssue]),
        };
        var review = new ClassReviewViewModel(new ClassDocumentPlan(roster, [firstRow, secondRow]), students, issues);

        Assert.Equal(2, review.StudentCount);
        Assert.Equal(1, review.MissingCount);
        Assert.Equal(1, review.DocumentWarningCount);
        Assert.Equal(0, review.OtherWarningCount);
        Assert.Equal("1 alumno sin histórico", review.MissingSummary);
        Assert.Equal("1 aviso documental", review.DocumentWarningSummary);
    }

    [Fact]
    public void StudentDetail_UnresolvedStudent_ExplainsClassAndGeneratedResult()
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "C");
        var row = MissingRow("ALUMNO, SIN HISTÓRICO", "87654321", ordinal: 1);
        var student = new StudentReviewViewModel(classIdentifier, row, []);

        var detail = student.Detail;

        Assert.Contains("Clase: 2ESOC", detail);
        Assert.Contains("No hay registros históricos candidatos", detail);
        Assert.Contains("las demás columnas quedarán vacías", detail);
    }

    private static StudentRowPlan MissingRow(string name, string studentNumber, int ordinal)
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
            new HistoricalResolution(HistoricalResolutionType.MissingHistoricalStudent, [], selectedCandidate: null));
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
