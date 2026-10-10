using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Validation;

namespace DocMerge.Application.Sorting;

/// <summary>Coordinates a validated, presentation-independent document sorting operation.</summary>
public sealed class SortDocumentService
{
    private readonly IDocumentSorter _sorter;
    private readonly IFileStore _fileStore;

    /// <summary>Initializes a new instance of the <see cref="SortDocumentService"/> class.</summary>
    public SortDocumentService(IDocumentSorter sorter, IFileStore fileStore)
    {
        _sorter = sorter ?? throw new ArgumentNullException(nameof(sorter));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    /// <summary>Validates and executes a document sorting request.</summary>
    /// <param name="request">The sorting request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The number of sorted person rows.</returns>
    public Task<int> ExecuteAsync(SortDocumentRequest request, CancellationToken cancellationToken)
    {
        OperationRequestValidator.ValidateSort(request, _fileStore);
        return _sorter.SortAsync(request, cancellationToken);
    }
}
