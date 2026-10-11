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

    /// <summary>Gets the historical source or a safe empty indicator.</summary>
    public string Source => Plan.Resolution.SelectedCandidate is null
        ? "—"
        : System.IO.Path.GetFileName(Plan.Resolution.SelectedCandidate.Record.DocumentPath);

    /// <summary>Gets a readable detail of fields and candidates.</summary>
    public string Detail => StudentReviewText.Create(Plan);
}

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

    /// <summary>Gets the number of warnings related to the class.</summary>
    public int WarningCount => Issues.Count(issue => !issue.IsBlocking);

    /// <summary>Gets the number of blocking issues related to the class.</summary>
    public int BlockingCount => Issues.Count(issue => issue.IsBlocking);

    /// <summary>Gets whether the class has at least one blocking issue.</summary>
    public bool HasBlockingIssues => BlockingCount > 0;

    /// <summary>Gets students whose historical information can be copied safely.</summary>
    public int RecoveredCount => Students.Count(student => student.Plan.Resolution.SelectedCandidate is not null);

    /// <summary>Gets students without a historical candidate.</summary>
    public int MissingCount => Students.Count(student =>
        student.Plan.Resolution.ResolutionType == HistoricalResolutionType.MissingHistoricalStudent);

    /// <summary>Gets students whose historical information cannot be copied safely.</summary>
    public int UnresolvedCount => Students.Count(student => student.Plan.Resolution.ResolutionType is
        HistoricalResolutionType.ConflictingHistoricalData
        or HistoricalResolutionType.AmbiguousStudentIdentity);
}

internal static class StudentReviewText
{
    internal static string Create(StudentRowPlan row)
    {
        var lines = new List<string>
        {
            $"Nombre en PDF: {row.RosterEntry.OriginalName}",
            $"NIA: {row.RosterEntry.StudentNumber}",
            $"Estado: {Status(row.Resolution.ResolutionType)}",
        };
        foreach (var candidate in row.Resolution.Candidates)
        {
            lines.Add(string.Empty);
            lines.Add($"Histórico: {candidate.Record.DocumentPath} ({candidate.Record.Location})");
            lines.AddRange(candidate.ReviewFields.Select(field =>
                $"{field.Header}: {field.Text}{(field.HasNonTextContent ? " [incluye contenido enriquecido]" : string.Empty)}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

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
