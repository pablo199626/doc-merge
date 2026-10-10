using System.Collections.ObjectModel;

namespace DocMerge.Domain.Pdf;

/// <summary>Represents the validated result of reading a PDF list.</summary>
public sealed record PdfReadResult
{
    /// <summary>Initializes a new instance of the <see cref="PdfReadResult"/> class.</summary>
    public PdfReadResult(
        IReadOnlyList<PdfEntry> entries,
        int declaredTotal,
        int pageCount,
        string sha256 = "")
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentOutOfRangeException.ThrowIfNegative(declaredTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        Entries = Array.AsReadOnly(entries.ToArray());
        DeclaredTotal = declaredTotal;
        PageCount = pageCount;
        Sha256 = sha256;
        Duplicates = CreateDuplicates(Entries);
    }

    /// <summary>Gets the extracted entries in declared order.</summary>
    public IReadOnlyList<PdfEntry> Entries { get; }

    /// <summary>Gets the total declared by the PDF.</summary>
    public int DeclaredTotal { get; }

    /// <summary>Gets the number of pages in the PDF.</summary>
    public int PageCount { get; }

    /// <summary>Gets the lowercase SHA-256 hash of the PDF.</summary>
    public string Sha256 { get; init; }

    /// <summary>Gets entries grouped by duplicated normalized identity.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PdfEntry>> Duplicates { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<PdfEntry>> CreateDuplicates(
        IReadOnlyList<PdfEntry> entries)
    {
        var duplicates = entries.GroupBy(entry => entry.IdentityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PdfEntry>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, IReadOnlyList<PdfEntry>>(duplicates);
    }
}
