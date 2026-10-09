namespace DocMerge.Domain.Pdf;

public sealed record PdfReadResult(
    IReadOnlyList<PdfEntry> Entries,
    int DeclaredTotal,
    int PageCount,
    string Sha256 = "")
{
    public IReadOnlyDictionary<string, IReadOnlyList<PdfEntry>> Duplicates => Entries
        .GroupBy(entry => entry.IdentityKey, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<PdfEntry>)group.ToArray(),
            StringComparer.Ordinal);
}
