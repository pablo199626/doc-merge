namespace DocMerge.Domain.Documents;

public sealed record DocumentRecord(
    string DocumentPath,
    int TableIndex,
    int RowIndex,
    string OriginalName,
    string IdentityKey,
    string RowXml,
    RecordDependencies Dependencies)
{
    public string Location => $"tabla {TableIndex}, fila {RowIndex}";
}
