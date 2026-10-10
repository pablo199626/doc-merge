namespace DocMerge.Application.Documents;

/// <summary>Sorts person rows while preserving the rest of a document package.</summary>
public interface IDocumentSorter
{
    /// <summary>Sorts a document according to the request.</summary>
    /// <param name="request">The sorting request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The number of sorted person rows.</returns>
    Task<int> SortAsync(
        SortDocumentRequest request,
        CancellationToken cancellationToken);
}
