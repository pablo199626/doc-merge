using DocMerge.Domain.Pdf;

namespace DocMerge.Application.Pdf;

public interface IPdfNameReader
{
    Task<PdfReadResult> ReadAsync(string path, CancellationToken cancellationToken);
}
