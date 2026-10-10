using DocMerge.Domain.Pdf;

namespace DocMerge.Application.Pdf;

/// <summary>Reads and validates the numbered person section of a PDF.</summary>
public interface IPdfNameReader
{
    /// <summary>Reads a PDF list.</summary>
    /// <param name="path">The PDF path.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated PDF result.</returns>
    Task<PdfReadResult> ReadAsync(string path, CancellationToken cancellationToken);
}
