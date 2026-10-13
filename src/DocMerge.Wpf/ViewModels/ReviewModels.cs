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
}

/// <summary>Represents one student row in the review table.</summary>
public sealed record StudentReviewViewModel(
    ClassIdentifier ClassIdentifier,
    StudentRowPlan Plan,
    IReadOnlyList<ReviewIssueViewModel> Issues)
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

    /// <summary>Gets a readable detail of fields and candidates.</summary>
    public string Detail => StudentReviewText.Create(ClassIdentifier, Plan, Issues);
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
    internal static string Create(
        ClassIdentifier classIdentifier,
        StudentRowPlan row,
        IReadOnlyList<ReviewIssueViewModel> issues)
    {
        var lines = new List<string>
        {
            "IDENTIDAD DEL LISTADO",
            $"Nombre en PDF: {row.RosterEntry.OriginalName}",
            $"NIA: {row.RosterEntry.StudentNumber}",
            $"Clase: {classIdentifier.CanonicalCode}",
            string.Empty,
            "RESOLUCIÓN",
            $"Estado: {Status(row.Resolution.ResolutionType)}",
            Consequence(row.Resolution.ResolutionType),
        };
        if (row.Resolution.Candidates.Count == 0)
        {
            lines.Add(string.Empty);
            lines.Add("PROCEDENCIA");
            lines.Add("No hay registros históricos candidatos.");
        }

        foreach (var candidate in row.Resolution.Candidates)
        {
            lines.Add(string.Empty);
            lines.Add(row.Resolution.SelectedCandidate == candidate
                ? "PROCEDENCIA SELECCIONADA"
                : "CANDIDATO NO SELECCIONADO");
            lines.Add($"Histórico: {candidate.Record.DocumentPath} ({candidate.Record.Location})");
            lines.AddRange(candidate.ReviewFields.Select(field =>
                $"{field.Header}: {field.Text}{(field.HasNonTextContent ? " [incluye contenido enriquecido]" : string.Empty)}"));
        }

        if (issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("INCIDENCIAS RELACIONADAS");
            lines.AddRange(issues.Select(issue =>
                $"• {issue.Title}: {issue.Issue.Detail} {issue.Consequence}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Consequence(HistoricalResolutionType type) => type switch
    {
        HistoricalResolutionType.Found =>
            "Se copiará la información del histórico seleccionado al documento generado.",
        HistoricalResolutionType.NewStudent =>
            "Se escribirá el nombre del PDF; las demás columnas quedarán vacías.",
        HistoricalResolutionType.Conflict =>
            "El alumno se omitirá del documento generado hasta que el conflicto se resuelva manualmente.",
        _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
    };

    internal static string Status(HistoricalResolutionType type) => type switch
    {
        HistoricalResolutionType.Found => "Encontrado",
        HistoricalResolutionType.NewStudent => "Alumno nuevo",
        HistoricalResolutionType.Conflict => "Conflicto",
        _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
    };
}
