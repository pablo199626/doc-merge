namespace DocMerge.Domain.Pdf;

public sealed record PdfEntry(
    string OriginalText,
    string IdentityKey,
    int PageNumber,
    int Ordinal)
{
    public string Location => $"página {PageNumber}, entrada {Ordinal}";
}
