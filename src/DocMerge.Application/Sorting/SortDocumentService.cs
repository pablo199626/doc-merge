using DocMerge.Application.Documents;
using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Sorting;

public sealed class SortDocumentService
{
    private readonly IDocumentEditor _editor;

    public SortDocumentService(IDocumentEditor editor)
    {
        _editor = editor;
    }

    public Task<int> ExecuteAsync(
        string inputPath,
        string outputPath,
        SortingProfile profile,
        string header,
        CancellationToken cancellationToken)
    {
        var request = new SortDocumentRequest(inputPath, outputPath, profile, header);
        return _editor.SortAsync(request, cancellationToken);
    }
}
