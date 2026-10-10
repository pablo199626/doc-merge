using System.Collections.ObjectModel;

namespace DocMerge.Domain.Documents;

/// <summary>Represents an immutable snapshot of the person table in a DOCX document.</summary>
public sealed record DocumentSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="DocumentSnapshot"/> class.</summary>
    public DocumentSnapshot(
        string documentPath,
        string sha256,
        int tableIndex,
        int headerIndex,
        int nameColumnIndex,
        IReadOnlyList<DocumentRecord> records,
        IReadOnlyList<AuxiliaryRow> auxiliaryRows)
    {
        ArgumentNullException.ThrowIfNull(documentPath);
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(auxiliaryRows);
        DocumentPath = documentPath;
        Sha256 = sha256;
        TableIndex = tableIndex;
        HeaderIndex = headerIndex;
        NameColumnIndex = nameColumnIndex;
        Records = Array.AsReadOnly(records.ToArray());
        AuxiliaryRows = Array.AsReadOnly(auxiliaryRows.ToArray());
        Duplicates = CreateDuplicates(Records);
    }

    /// <summary>Gets the source document path.</summary>
    public string DocumentPath { get; }

    /// <summary>Gets the lowercase SHA-256 hash of the document.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the zero-based index of the person table.</summary>
    public int TableIndex { get; }

    /// <summary>Gets the zero-based index of the header row.</summary>
    public int HeaderIndex { get; }

    /// <summary>Gets the zero-based index of the identity column.</summary>
    public int NameColumnIndex { get; }

    /// <summary>Gets the person records in document order.</summary>
    public IReadOnlyList<DocumentRecord> Records { get; }

    /// <summary>Gets the non-person rows following the header.</summary>
    public IReadOnlyList<AuxiliaryRow> AuxiliaryRows { get; }

    /// <summary>Gets records grouped by duplicated normalized identity.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DocumentRecord>> Duplicates { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<DocumentRecord>> CreateDuplicates(
        IReadOnlyList<DocumentRecord> records)
    {
        var duplicates = records.GroupBy(record => record.IdentityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DocumentRecord>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, IReadOnlyList<DocumentRecord>>(duplicates);
    }
}
