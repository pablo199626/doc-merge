namespace DocMerge.Domain.Generation;

/// <summary>Identifies one source assignment or document affected by an issue.</summary>
/// <param name="SourcePath">The source document path.</param>
/// <param name="Location">The readable location within the source.</param>
/// <param name="ClassIdentifier">The affected class, when applicable.</param>
/// <param name="StudentNumber">The affected student number, when applicable.</param>
public sealed record GenerationIssueReference(
    string SourcePath,
    string Location,
    ClassIdentifier? ClassIdentifier = null,
    string? StudentNumber = null);
