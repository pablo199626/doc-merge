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

    /// <summary>Gets the number of document-level warnings related to the class.</summary>
    public int DocumentWarningCount => Issues.Count(issue =>
        !issue.IsBlocking && issue.Issue.Code == "PdfWithoutDeclaredTotal");

    /// <summary>Gets the number of other non-blocking issues related to the class.</summary>
    public int OtherWarningCount => Issues.Count(issue =>
        !issue.IsBlocking && issue.Issue.Code is not
            "MissingHistoricalStudent" and not
            "ConflictingHistoricalData" and not
            "AmbiguousStudentIdentity" and not
            "PdfWithoutDeclaredTotal");

    /// <summary>Gets the number of blocking issues related to the class.</summary>
    public int BlockingCount => Issues.Count(issue => issue.IsBlocking);

    /// <summary>Gets whether the class has at least one blocking issue.</summary>
    public bool HasBlockingIssues => BlockingCount > 0;

    /// <summary>Gets whether the class has any issue in the batch report.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets whether the class has a document-level warning.</summary>
    public bool HasDocumentWarnings => DocumentWarningCount > 0;

    /// <summary>Gets whether the class has another non-blocking warning.</summary>
    public bool HasOtherWarnings => OtherWarningCount > 0;

    /// <summary>Gets students whose historical information can be copied safely.</summary>
    public int RecoveredCount => CountDistinctStudents(student =>
        student.ResolutionType is HistoricalResolutionType.UniqueMatch
            or HistoricalResolutionType.EquivalentMatches);

    /// <summary>Gets whether the class has safely recovered students.</summary>
    public bool HasRecoveredStudents => RecoveredCount > 0;

    /// <summary>Gets students without a historical candidate.</summary>
    public int MissingCount => CountDistinctStudents(student =>
        student.ResolutionType == HistoricalResolutionType.MissingHistoricalStudent);

    /// <summary>Gets whether the class has students without historical information.</summary>
    public bool HasMissingStudents => MissingCount > 0;

    /// <summary>Gets students whose historical information cannot be copied safely.</summary>
    public int UnresolvedCount => CountDistinctStudents(student => student.ResolutionType is
        HistoricalResolutionType.ConflictingHistoricalData
        or HistoricalResolutionType.AmbiguousStudentIdentity);

    /// <summary>Gets whether the class has students whose identity or history is unresolved.</summary>
    public bool HasUnresolvedStudents => UnresolvedCount > 0;

    /// <summary>Gets an accessible summary of students without historical information.</summary>
    public string MissingSummary => CountText(MissingCount, "alumno sin histórico", "alumnos sin histórico");

    /// <summary>Gets an accessible summary of safely recovered students.</summary>
    public string RecoveredSummary => CountText(RecoveredCount, "alumno recuperado", "alumnos recuperados");

    /// <summary>Gets an accessible summary of unresolved students.</summary>
    public string UnresolvedSummary => CountText(UnresolvedCount, "conflicto", "conflictos");

    /// <summary>Gets an accessible summary of document-level warnings.</summary>
    public string DocumentWarningSummary => CountText(DocumentWarningCount, "aviso documental", "avisos documentales");

    /// <summary>Gets an accessible summary of other warnings.</summary>
    public string OtherWarningSummary => CountText(OtherWarningCount, "otro aviso", "otros avisos");

    /// <summary>Gets an accessible summary of blocking issues.</summary>
    public string BlockingSummary => CountText(BlockingCount, "bloqueo", "bloqueos");

    /// <summary>Gets issues associated with missing historical students.</summary>
    public ClassIssueGroupViewModel MissingIssues => CreateIssueGroup(
        $"Alumnos sin histórico de {Code}",
        issue => issue.Issue.Code == "MissingHistoricalStudent");

    /// <summary>Gets issues associated with conflicting or ambiguous students.</summary>
    public ClassIssueGroupViewModel UnresolvedIssues => CreateIssueGroup(
        $"Conflictos de {Code}",
        issue => issue.Issue.Code is "ConflictingHistoricalData" or "AmbiguousStudentIdentity");

    /// <summary>Gets document-level warnings.</summary>
    public ClassIssueGroupViewModel DocumentIssues => CreateIssueGroup(
        $"Avisos documentales de {Code}",
        issue => issue.Issue.Code == "PdfWithoutDeclaredTotal");

    /// <summary>Gets other non-blocking warnings.</summary>
    public ClassIssueGroupViewModel OtherIssues => CreateIssueGroup(
        $"Otros avisos de {Code}",
        issue => !issue.IsBlocking && issue.Issue.Code is not
            "MissingHistoricalStudent" and not
            "ConflictingHistoricalData" and not
            "AmbiguousStudentIdentity" and not
            "PdfWithoutDeclaredTotal");

    /// <summary>Gets blocking issues.</summary>
    public ClassIssueGroupViewModel BlockingIssues => CreateIssueGroup(
        $"Bloqueos de {Code}",
        issue => issue.IsBlocking);

    /// <summary>Gets every issue associated with the class.</summary>
    public ClassIssueGroupViewModel AllIssues => new($"Incidencias de {Code}", Issues);

    private int CountDistinctStudents(Func<StudentReviewViewModel, bool> predicate) =>
        Students.Where(predicate)
            .Select(student => student.StudentNumber)
            .Distinct(StringComparer.Ordinal)
            .Count();

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
        HistoricalResolutionType.UniqueMatch =>
            "Se copiará la información del histórico seleccionado al documento generado.",
        HistoricalResolutionType.EquivalentMatches =>
            "Se copiará una representación segura de los históricos equivalentes.",
        HistoricalResolutionType.MissingHistoricalStudent =>
            "Se escribirá el nombre del PDF; las demás columnas quedarán vacías y señalizadas.",
        HistoricalResolutionType.ConflictingHistoricalData =>
            "No se copiará información dudosa. Se escribirá el nombre del PDF y se señalizarán las columnas vacías.",
        HistoricalResolutionType.AmbiguousStudentIdentity =>
            "No se elegirá ningún candidato. Se escribirá el nombre del PDF y se señalizarán las columnas vacías.",
        _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
    };

    internal static string Status(HistoricalResolutionType type) => type switch
    {
        HistoricalResolutionType.UniqueMatch => "Recuperado",
        HistoricalResolutionType.EquivalentMatches => "Recuperado · históricos equivalentes",
        HistoricalResolutionType.MissingHistoricalStudent => "Sin histórico",
        HistoricalResolutionType.ConflictingHistoricalData => "Conflicto histórico",
        HistoricalResolutionType.AmbiguousStudentIdentity => "Identidad ambigua",
        _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
    };
}
