using DocMerge.Domain.Documents;

namespace DocMerge.Application.Documents;

public interface IDocumentReader
{
    Task<DocumentSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken);
}
