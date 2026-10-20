using System.Collections.ObjectModel;

namespace DocMerge.Application.Documents;

/// <summary>Describes the validated student table and prototype row of a DOCX template.</summary>
public sealed record GenerationTemplateSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="GenerationTemplateSnapshot"/> class.</summary>
    /// <param name="documentPath">The template path.</param>
    /// <param name="sha256">The lowercase SHA-256 hash of the template.</param>
    /// <param name="tableIndex">The zero-based student-table index.</param>
    /// <param name="headerIndex">The zero-based header-row index.</param>
    /// <param name="nameColumnIndex">The zero-based student-name column index.</param>
    /// <param name="columnHeaders">The normalized visible column headers.</param>
    /// <param name="prototypeRowXml">The serialized empty prototype row.</param>
    /// <param name="headerContractVersion">The supported document-header contract, when present.</param>
    public GenerationTemplateSnapshot(
        string documentPath,
        string sha256,
        int tableIndex,
        int headerIndex,
        int nameColumnIndex,
        IReadOnlyList<string> columnHeaders,
        string prototypeRowXml,
        int? headerContractVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(columnHeaders);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeRowXml);
        DocumentPath = documentPath;
        Sha256 = sha256;
        TableIndex = tableIndex;
        HeaderIndex = headerIndex;
        NameColumnIndex = nameColumnIndex;
        ColumnHeaders = new ReadOnlyCollection<string>(columnHeaders.ToArray());
        PrototypeRowXml = prototypeRowXml;
        HeaderContractVersion = headerContractVersion;
    }

    /// <summary>Gets the template path.</summary>
    public string DocumentPath { get; }

    /// <summary>Gets the lowercase SHA-256 hash of the template.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the zero-based student-table index.</summary>
    public int TableIndex { get; }

    /// <summary>Gets the zero-based header-row index.</summary>
    public int HeaderIndex { get; }

    /// <summary>Gets the zero-based student-name column index.</summary>
    public int NameColumnIndex { get; }

    /// <summary>Gets the normalized visible column headers.</summary>
    public IReadOnlyList<string> ColumnHeaders { get; }

    /// <summary>Gets the serialized empty prototype row.</summary>
    public string PrototypeRowXml { get; }

    /// <summary>Gets the supported document-header contract, or <see langword="null"/> for a legacy template.</summary>
    public int? HeaderContractVersion { get; }

    /// <summary>Gets whether the template supports class-title and tutor personalization.</summary>
    public bool SupportsDocumentHeader => HeaderContractVersion == TemplateHeaderContract.Version;
}
