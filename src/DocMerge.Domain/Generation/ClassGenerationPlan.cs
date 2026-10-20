namespace DocMerge.Domain.Generation;

/// <summary>Represents the immutable plan for a complete class-generation batch.</summary>
public sealed record ClassGenerationPlan
{
    /// <summary>Initializes a new instance of the <see cref="ClassGenerationPlan"/> class.</summary>
    /// <param name="classes">The class-document plans.</param>
    /// <param name="issues">The structured planning issues.</param>
    public ClassGenerationPlan(
        IReadOnlyList<ClassDocumentPlan> classes,
        IReadOnlyList<GenerationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(issues);
        Classes = Array.AsReadOnly(classes.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
        OutputClasses = Array.AsReadOnly(Classes.Where(classPlan => classPlan.ProducesDocument).ToArray());
        RosterStudentCount = Classes.Sum(classPlan => classPlan.Rows.Count);
        IncludedStudentCount = Classes.Sum(classPlan => classPlan.IncludedRows.Count);
        OmittedStudentCount = RosterStudentCount - IncludedStudentCount;
    }

    /// <summary>Gets the planned class documents.</summary>
    public IReadOnlyList<ClassDocumentPlan> Classes { get; }

    /// <summary>Gets the structured planning issues.</summary>
    public IReadOnlyList<GenerationIssue> Issues { get; }

    /// <summary>Gets classes that contain at least one student eligible for output.</summary>
    public IReadOnlyList<ClassDocumentPlan> OutputClasses { get; }

    /// <summary>Gets the number of source roster assignments.</summary>
    public int RosterStudentCount { get; }

    /// <summary>Gets the number of students that will be written.</summary>
    public int IncludedStudentCount { get; }

    /// <summary>Gets the number of students omitted because of conflicts.</summary>
    public int OmittedStudentCount { get; }

    /// <summary>Gets a value that indicates whether final publication is blocked.</summary>
    public bool HasBlockingIssues => Issues.Any(
        issue => issue.Severity == GenerationIssueSeverity.Blocking);
}
