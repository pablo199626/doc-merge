namespace DocMerge.Domain.Documents;

public sealed record RelationshipReference(
    string RelationshipId,
    string RelationshipType,
    string Target,
    bool IsExternal);
