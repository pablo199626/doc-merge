namespace DocMerge.Domain.Documents;

/// <summary>Represents one person row and its package dependencies.</summary>
/// <param name="DocumentPath">The path of the document containing the row.</param>
/// <param name="TableIndex">The zero-based index of the containing table.</param>
/// <param name="RowIndex">The zero-based index of the row.</param>
/// <param name="OriginalName">The original identity text.</param>
/// <param name="IdentityKey">The normalized identity key.</param>
/// <param name="RowXml">The serialized WordprocessingML row.</param>
/// <param name="Dependencies">The dependencies referenced by the row.</param>
public sealed record DocumentRecord(
    string DocumentPath,
    int TableIndex,
    int RowIndex,
    string OriginalName,
    string IdentityKey,
    string RowXml,
    RecordDependencies Dependencies)
{
    /// <summary>Gets a human-readable location within the document.</summary>
    public string Location => $"tabla {TableIndex}, fila {RowIndex}";
}
