using DocMerge.Domain.Documents;

namespace DocMerge.Application.Documents;

/// <summary>Reads validated document snapshots from external storage.</summary>
public interface IDocumentReader
{
    /// <summary>Reads the person table and its package dependencies.</summary>
    /// <param name="path">The DOCX path.</param>
    /// <param name="header">The exact identity-column header.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The immutable document snapshot.</returns>
    Task<DocumentSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken);
}
