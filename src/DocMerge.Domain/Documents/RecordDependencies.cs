namespace DocMerge.Domain.Documents;

/// <summary>Describes styles, numbering, and relationships referenced by a row.</summary>
public sealed record RecordDependencies
{
    /// <summary>Gets an empty dependency set.</summary>
    public static readonly RecordDependencies Empty = new([], [], []);

    /// <summary>Initializes a new instance of the <see cref="RecordDependencies"/> class.</summary>
    public RecordDependencies(
        IReadOnlyList<string> styles,
        IReadOnlyList<string> numbering,
        IReadOnlyList<RelationshipReference> relationships)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(numbering);
        ArgumentNullException.ThrowIfNull(relationships);
        Styles = Array.AsReadOnly(styles.ToArray());
        Numbering = Array.AsReadOnly(numbering.ToArray());
        Relationships = Array.AsReadOnly(relationships.ToArray());
    }

    /// <summary>Gets the referenced style identifiers.</summary>
    public IReadOnlyList<string> Styles { get; }

    /// <summary>Gets the referenced numbering identifiers.</summary>
    public IReadOnlyList<string> Numbering { get; }

    /// <summary>Gets the referenced package relationships.</summary>
    public IReadOnlyList<RelationshipReference> Relationships { get; }
}
