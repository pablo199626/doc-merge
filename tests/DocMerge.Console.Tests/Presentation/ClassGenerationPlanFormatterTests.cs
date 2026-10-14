using DocMerge.Console.Presentation;
using DocMerge.Domain.Generation;

namespace DocMerge.Console.Tests.Presentation;

public sealed class ClassGenerationPlanFormatterTests
{
    [Fact]
    public void Format_NewAndConflictedStudents_ReportsOutputConsequences()
    {
        var identifier = new ClassIdentifier("ESO", 2, "A");
        var newStudent = Row("11111111", "ALUMNO, NUEVO", HistoricalResolutionType.NewStudent);
        var conflict = Row("22222222", "ALUMNO, DUPLICADO", HistoricalResolutionType.Conflict);
        var roster = new ClassRoster("2ESOA.pdf", "hash", identifier, [newStudent.RosterEntry, conflict.RosterEntry], 1, false);
        var issue = new GenerationIssue(
            "DuplicateStudentName",
            GenerationIssueSeverity.Warning,
            identifier,
            conflict.RosterEntry.OriginalName,
            "Nombre duplicado.",
            ["2ESOA.pdf"],
            [new GenerationIssueReference("2ESOA.pdf", conflict.RosterEntry.Location, identifier, "22222222")]);
        var plan = new ClassGenerationPlan([new ClassDocumentPlan(roster, [newStudent, conflict])], [issue]);

        var result = ClassGenerationPlanFormatter.Format(plan, simulation: true);

        Assert.Contains("nuevos=1", result, StringComparison.Ordinal);
        Assert.Contains("conflictos_omitidos=1", result, StringComparison.Ordinal);
        Assert.Contains("documento=sí", result, StringComparison.Ordinal);
        Assert.Contains("DuplicateStudentName", result, StringComparison.Ordinal);
    }

    private static StudentRowPlan Row(
        string studentNumber,
        string name,
        HistoricalResolutionType resolutionType)
    {
        var entry = new ClassRosterEntry(
            name,
            name,
            StudentNameMatchKey.Create(name),
            studentNumber,
            1,
            1);
        return new StudentRowPlan(entry, new HistoricalResolution(resolutionType, [], null));
    }
}
