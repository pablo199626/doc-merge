namespace DocMerge.Domain.Documents;

public sealed record RecordDependencies(
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Numbering,
    IReadOnlyList<RelationshipReference> Relationships)
{
    public static readonly RecordDependencies Empty = new([], [], []);
}
