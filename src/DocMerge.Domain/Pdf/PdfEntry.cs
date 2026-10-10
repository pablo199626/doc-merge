namespace DocMerge.Domain.Pdf;

/// <summary>Represents one numbered person entry extracted from a PDF list.</summary>
/// <param name="OriginalText">The original extracted name.</param>
/// <param name="IdentityKey">The normalized identity key.</param>
/// <param name="PageNumber">The one-based source page number.</param>
/// <param name="Ordinal">The declared ordinal in the list.</param>
public sealed record PdfEntry(
    string OriginalText,
    string IdentityKey,
    int PageNumber,
    int Ordinal)
{
    /// <summary>Gets a human-readable location in the PDF.</summary>
    public string Location => $"página {PageNumber}, entrada {Ordinal}";
}
