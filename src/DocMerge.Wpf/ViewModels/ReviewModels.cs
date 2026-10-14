using DocMerge.Domain.Generation;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Represents one issue prepared for desktop review.</summary>
public sealed record ReviewIssueViewModel(
    GenerationIssue Issue,
    string Title,
    string Scope,
    string Consequence,
    bool IsBlocking)
{
    /// <summary>Gets a readable severity.</summary>
    public string Severity => IsBlocking ? "Bloqueo" : "Aviso";

    /// <summary>Gets structured document references suitable for review.</summary>
    public IReadOnlyList<ReviewIssueSourceViewModel> Sources => Issue.SourceReferences.Count > 0
        ? Issue.SourceReferences.Select(reference => new ReviewIssueSourceViewModel(
            System.IO.Path.GetFileName(reference.SourcePath),
            reference.SourcePath,
            reference.Location)).ToArray()
        : Issue.References.Select(reference => new ReviewIssueSourceViewModel(
            reference,
            string.Empty,
            string.Empty)).ToArray();
}

/// <summary>Represents one traceable source reference attached to an issue.</summary>
/// <param name="DisplayName">The source file name or fallback reference.</param>
/// <param name="Path">The complete source path, when available.</param>
/// <param name="Location">The readable location within the source.</param>
public sealed record ReviewIssueSourceViewModel(
    string DisplayName,
    string Path,
    string Location);

/// <summary>Represents one student row in the review table.</summary>
public sealed record StudentReviewViewModel(
    ClassIdentifier ClassIdentifier,
    StudentRowPlan Plan,
    IReadOnlyList<ReviewIssueViewModel> Issues,
    string RosterSourcePath = "")
{
    /// <summary>Gets the PDF student name.</summary>
    public string Name => Plan.RosterEntry.OriginalName;

    /// <summary>Gets the student number from the PDF.</summary>
    public string StudentNumber => Plan.RosterEntry.StudentNumber;

    /// <summary>Gets the original one-based PDF order.</summary>
    public int Ordinal => Plan.RosterEntry.Ordinal;

    /// <summary>Gets the readable historical resolution.</summary>
    public string Status => StudentReviewText.Status(Plan.Resolution.ResolutionType);

    /// <summary>Gets the typed historical resolution for presentation styling.</summary>
    public HistoricalResolutionType ResolutionType => Plan.Resolution.ResolutionType;

    /// <summary>Gets the historical source or a safe empty indicator.</summary>
    public string Source => Plan.Resolution.SelectedCandidate is null
        ? "—"
        : System.IO.Path.GetFileName(Plan.Resolution.SelectedCandidate.Record.DocumentPath);

}

/// <summary>Represents a traceable subset of issues for one class indicator.</summary>
public sealed record ClassIssueGroupViewModel(
    string Title,
    IReadOnlyList<ReviewIssueViewModel> Issues);

/// <summary>Represents one class in the review list.</summary>
public sealed record ClassReviewViewModel(
    ClassDocumentPlan Plan,
    IReadOnlyList<StudentReviewViewModel> Students,
    IReadOnlyList<ReviewIssueViewModel> Issues)
{
    /// <summary>Gets the canonical class code.</summary>
    public string Code => Plan.Roster.ClassIdentifier.CanonicalCode;

    /// <summary>Gets the number of PDF assignments.</summary>
    public int StudentCount => Students.Count;

    /// <summary>Gets an accessible summary of all assignments in the class.</summary>
    public string StudentSummary => CountText(StudentCount, "alumno", "alumnos");

    /// <summary>Gets the number of blocking issues related to the class.</summary>
    public int BlockingCount => Issues.Count(issue => issue.IsBlocking);

    /// <summary>Gets whether the class has at least one blocking issue.</summary>
    public bool HasBlockingIssues => BlockingCount > 0;

    /// <summary>Gets whether the class has any issue in the batch report.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets students with exactly one historical match.</summary>
    public int FoundCount => Students.Count(student =>
        student.ResolutionType == HistoricalResolutionType.Found);

    /// <summary>Gets whether the class has found students.</summary>
    public bool HasFoundStudents => FoundCount > 0;

    /// <summary>Gets students with no historical match.</summary>
    public int NewStudentCount => Students.Count(student =>
        student.ResolutionType == HistoricalResolutionType.NewStudent);

    /// <summary>Gets whether the class has new students.</summary>
    public bool HasNewStudents => NewStudentCount > 0;

    /// <summary>Gets students omitted because their name cannot be resolved uniquely.</summary>
    public int ConflictCount => Students.Count(student =>
        student.ResolutionType == HistoricalResolutionType.Conflict);

    /// <summary>Gets whether the class has conflicted students.</summary>
    public bool HasConflicts => ConflictCount > 0;

    /// <summary>Gets students that will be included in the generated document.</summary>
    public int IncludedCount => StudentCount - ConflictCount;

    /// <summary>Gets the complete output summary for this class.</summary>
    public string OutputSummary => $"{CountText(StudentCount, "alumno en el listado", "alumnos en el listado")} · "
        + $"{IncludedCount} {(IncludedCount == 1 ? "se incluirá" : "se incluirán")} · "
        + $"{ConflictCount} {(ConflictCount == 1 ? "omitido" : "omitidos")} por conflicto";

    /// <summary>Gets an accessible summary of found students.</summary>
    public string FoundSummary => CountText(FoundCount, "alumno encontrado", "alumnos encontrados");

    /// <summary>Gets an accessible summary of new students.</summary>
    public string NewStudentSummary => CountText(NewStudentCount, "alumno nuevo", "alumnos nuevos");

    /// <summary>Gets an accessible summary of conflicted students.</summary>
    public string ConflictSummary => CountText(ConflictCount, "alumno en conflicto", "alumnos en conflicto");

    /// <summary>Gets an accessible summary of blocking issues.</summary>
    public string BlockingSummary => CountText(BlockingCount, "bloqueo", "bloqueos");

    /// <summary>Gets issues associated with duplicated student names.</summary>
    public ClassIssueGroupViewModel ConflictIssues => CreateIssueGroup(
        $"Conflictos de {Code}",
        issue => issue.Issue.Code == "DuplicateStudentName");

    /// <summary>Gets blocking issues.</summary>
    public ClassIssueGroupViewModel BlockingIssues => CreateIssueGroup(
        $"Bloqueos de {Code}",
        issue => issue.IsBlocking);

    /// <summary>Gets every issue associated with the class.</summary>
    public ClassIssueGroupViewModel AllIssues => new($"Incidencias de {Code}", Issues);

    private static string CountText(int count, string singular, string plural) =>
        $"{count} {(count == 1 ? singular : plural)}";

    private ClassIssueGroupViewModel CreateIssueGroup(
        string title,
        Func<ReviewIssueViewModel, bool> predicate) => new(
            title,
            Issues.Where(predicate).ToArray());
}

internal static class StudentReviewText
{
    internal static string Status(HistoricalResolutionType type) => type switch
    {
        HistoricalResolutionType.Found => "Encontrado",
        HistoricalResolutionType.NewStudent => "Alumno nuevo",
        HistoricalResolutionType.Conflict => "Conflicto",
        _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
    };
}
