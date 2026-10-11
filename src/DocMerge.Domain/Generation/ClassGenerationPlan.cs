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
    }

    /// <summary>Gets the planned class documents.</summary>
    public IReadOnlyList<ClassDocumentPlan> Classes { get; }

    /// <summary>Gets the structured planning issues.</summary>
    public IReadOnlyList<GenerationIssue> Issues { get; }

    /// <summary>Gets a value that indicates whether final publication is blocked.</summary>
    public bool HasBlockingIssues => Issues.Any(
        issue => issue.Severity == GenerationIssueSeverity.Blocking);
}
