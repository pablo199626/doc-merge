namespace DocMerge.Domain.Documents;

/// <summary>Represents an OPC relationship referenced by a document row.</summary>
/// <param name="RelationshipId">The relationship identifier used in the row.</param>
/// <param name="RelationshipType">The relationship type URI.</param>
/// <param name="Target">The resolved package path or external URI.</param>
/// <param name="IsExternal">Whether the relationship targets an external resource.</param>
public sealed record RelationshipReference(
    string RelationshipId,
    string RelationshipType,
    string Target,
    bool IsExternal);
