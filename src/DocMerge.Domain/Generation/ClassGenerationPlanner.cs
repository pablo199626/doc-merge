namespace DocMerge.Domain.Generation;

/// <summary>Builds deterministic class-document plans from PDF rosters and historical data.</summary>
public sealed class ClassGenerationPlanner
{
    private readonly HistoricalStudentResolver _resolver;

    /// <summary>Initializes a new instance of the <see cref="ClassGenerationPlanner"/> class.</summary>
    /// <param name="resolver">The historical student resolver.</param>
    public ClassGenerationPlanner(HistoricalStudentResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>Creates a complete generation plan.</summary>
    /// <param name="rosters">The validated PDF rosters in deterministic order.</param>
    /// <param name="index">The complete historical student index.</param>
    /// <returns>The immutable generation plan and its issues.</returns>
    public ClassGenerationPlan Create(
        IReadOnlyList<ClassRoster> rosters,
        HistoricalStudentIndex index)
    {
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(index);
        var issues = FindGlobalIssues(rosters);
        var ambiguousNames = FindAmbiguousNameKeys(rosters);
        var classes = rosters.Select(roster => CreateClassPlan(
            roster, index, ambiguousNames, issues)).ToArray();
        return new ClassGenerationPlan(classes, issues);
    }

    private ClassDocumentPlan CreateClassPlan(
        ClassRoster roster,
        HistoricalStudentIndex index,
        IReadOnlySet<string> ambiguousNames,
        ICollection<GenerationIssue> issues)
    {
        var rows = new List<StudentRowPlan>(roster.Entries.Count);
        foreach (var entry in roster.Entries)
        {
            var resolution = ambiguousNames.Contains(entry.MatchKey)
                ? CreateAmbiguousResolution(entry, index)
                : _resolver.Resolve(entry.MatchKey, index);
            rows.Add(new StudentRowPlan(entry, resolution));
            AddResolutionIssue(roster.ClassIdentifier, entry, resolution, issues);
        }

        return new ClassDocumentPlan(roster, rows);
    }

    private static HistoricalResolution CreateAmbiguousResolution(
        ClassRosterEntry entry,
        HistoricalStudentIndex index)
    {
        return new HistoricalResolution(
            HistoricalResolutionType.AmbiguousStudentIdentity,
            index.Find(entry.MatchKey),
            null);
    }

    private static List<GenerationIssue> FindGlobalIssues(IReadOnlyList<ClassRoster> rosters)
    {
        var issues = rosters.GroupBy(roster => roster.ClassIdentifier)
            .Where(group => group.Count() > 1)
            .Select(group => new GenerationIssue(
                "DuplicateClass",
                GenerationIssueSeverity.Blocking,
                group.Key,
                null,
                "Varios PDF representan la misma clase.",
                group.Select(roster => roster.SourcePath).ToArray()))
            .ToList();
        issues.AddRange(rosters.Where(roster => !roster.HasDeclaredTotal)
            .Select(roster => new GenerationIssue(
                "PdfWithoutDeclaredTotal",
                GenerationIssueSeverity.Warning,
                roster.ClassIdentifier,
                null,
                "El PDF no declara un total independiente; se aplicó validación estructural estricta.",
                [roster.SourcePath])));
        issues.AddRange(FindDuplicateStudentNumberIssues(rosters));
        return issues;
    }

    private static IEnumerable<GenerationIssue> FindDuplicateStudentNumberIssues(
        IReadOnlyList<ClassRoster> rosters)
    {
        return rosters.SelectMany(roster => roster.Entries.Select(entry => (roster, entry)))
            .GroupBy(item => item.entry.StudentNumber, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new GenerationIssue(
                "DuplicateStudentNumber",
                GenerationIssueSeverity.Blocking,
                null,
                group.First().entry.OriginalName,
                $"El NIA {group.Key} aparece en varias asignaciones PDF.",
                group.Select(item =>
                    $"{item.roster.SourcePath}: {item.entry.Location}").ToArray()));
    }

    private static HashSet<string> FindAmbiguousNameKeys(IReadOnlyList<ClassRoster> rosters)
    {
        return rosters.SelectMany(roster => roster.Entries)
            .GroupBy(entry => entry.MatchKey, StringComparer.Ordinal)
            .Where(group => group.Select(entry => entry.StudentNumber)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AddResolutionIssue(
        ClassIdentifier classIdentifier,
        ClassRosterEntry entry,
        HistoricalResolution resolution,
        ICollection<GenerationIssue> issues)
    {
        var issue = resolution.ResolutionType switch
        {
            HistoricalResolutionType.UniqueMatch => null,
            HistoricalResolutionType.EquivalentMatches => CreateResolutionIssue(
                "EquivalentHistoricalRecords", GenerationIssueSeverity.Warning,
                "Varios registros históricos equivalentes se consolidarán.",
                classIdentifier, entry, resolution),
            HistoricalResolutionType.MissingHistoricalStudent => CreateResolutionIssue(
                "MissingHistoricalStudent", GenerationIssueSeverity.Warning,
                "El alumno no aparece en los documentos históricos.",
                classIdentifier, entry, resolution),
            HistoricalResolutionType.ConflictingHistoricalData => CreateResolutionIssue(
                "ConflictingHistoricalData", GenerationIssueSeverity.Warning,
                "Los documentos históricos contienen información contradictoria.",
                classIdentifier, entry, resolution),
            HistoricalResolutionType.AmbiguousStudentIdentity => CreateResolutionIssue(
                "AmbiguousStudentIdentity", GenerationIssueSeverity.Warning,
                "El mismo nombre corresponde a distintos NIA en los PDF.",
                classIdentifier, entry, resolution),
            _ => throw new InvalidOperationException("Resolución histórica no reconocida."),
        };
        if (issue is not null)
        {
            issues.Add(issue);
        }
    }

    private static GenerationIssue CreateResolutionIssue(
        string code,
        GenerationIssueSeverity severity,
        string detail,
        ClassIdentifier classIdentifier,
        ClassRosterEntry entry,
        HistoricalResolution resolution)
    {
        var references = new[] { entry.Location }
            .Concat(resolution.Candidates.Select(candidate =>
                $"{candidate.Record.DocumentPath}: {candidate.Record.Location}"))
            .ToArray();
        return new GenerationIssue(
            code, severity, classIdentifier, entry.OriginalName, detail, references);
    }
}
