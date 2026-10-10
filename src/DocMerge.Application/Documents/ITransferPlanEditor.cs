namespace DocMerge.Application.Documents;

/// <summary>Applies transfer actions and imports their package dependencies.</summary>
public interface ITransferPlanEditor
{
    /// <summary>Creates an unsorted temporary document from a transfer plan.</summary>
    /// <param name="request">The transfer editing request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The number of rows added after applying replacements and deletions.</returns>
    Task<int> ApplyTransferPlanAsync(
        ApplyTransferPlanRequest request,
        CancellationToken cancellationToken);
}
