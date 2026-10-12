namespace DocMerge.Domain.Generation;

/// <summary>Represents one structured, presentation-independent generation issue.</summary>
/// <param name="Code">The stable machine-readable issue code.</param>
/// <param name="Severity">The issue severity.</param>
/// <param name="ClassIdentifier">The affected class, when applicable.</param>
/// <param name="StudentName">The affected student name, when applicable.</param>
/// <param name="Detail">The user-facing detail.</param>
/// <param name="References">The traceable source references.</param>
/// <param name="StructuredReferences">Structured source references for unambiguous presentation.</param>
public sealed record GenerationIssue(
    string Code,
    GenerationIssueSeverity Severity,
    ClassIdentifier? ClassIdentifier,
    string? StudentName,
    string Detail,
    IReadOnlyList<string> References,
    IReadOnlyList<GenerationIssueReference>? StructuredReferences = null)
{
    /// <summary>Gets immutable structured references for this issue.</summary>
    public IReadOnlyList<GenerationIssueReference> SourceReferences { get; } =
        Array.AsReadOnly((StructuredReferences ?? []).ToArray());
}
