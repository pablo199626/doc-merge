namespace DocMerge.Domain.Generation;

/// <summary>Represents one structured, presentation-independent generation issue.</summary>
/// <param name="Code">The stable machine-readable issue code.</param>
/// <param name="Severity">The issue severity.</param>
/// <param name="ClassIdentifier">The affected class, when applicable.</param>
/// <param name="StudentName">The affected student name, when applicable.</param>
/// <param name="Detail">The user-facing detail.</param>
/// <param name="References">The traceable source references.</param>
public sealed record GenerationIssue(
    string Code,
    GenerationIssueSeverity Severity,
    ClassIdentifier? ClassIdentifier,
    string? StudentName,
    string Detail,
    IReadOnlyList<string> References);
