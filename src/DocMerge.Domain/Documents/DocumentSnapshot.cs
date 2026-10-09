namespace DocMerge.Domain.Documents;

public sealed record DocumentSnapshot(
    string DocumentPath,
    string Sha256,
    int TableIndex,
    int HeaderIndex,
    int NameColumnIndex,
    IReadOnlyList<DocumentRecord> Records,
    IReadOnlyList<AuxiliaryRow> AuxiliaryRows)
{
    public IReadOnlyDictionary<string, IReadOnlyList<DocumentRecord>> Duplicates => Records
        .GroupBy(record => record.IdentityKey, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<DocumentRecord>)group.ToArray(),
            StringComparer.Ordinal);
}
