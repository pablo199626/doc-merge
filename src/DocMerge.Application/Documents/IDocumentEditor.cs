namespace DocMerge.Application.Documents;

public interface IDocumentEditor
{
    Task<int> SortAsync(
        SortDocumentRequest request,
        CancellationToken cancellationToken);

    Task<int> ApplyTransferPlanAsync(
        ApplyTransferPlanRequest request,
        CancellationToken cancellationToken);
}
