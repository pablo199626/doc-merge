namespace DocMerge.Application.Generation;

/// <summary>Generates temporary class documents from a prepared immutable plan.</summary>
public interface IClassDocumentGenerator
{
    /// <summary>Generates all class documents without publishing them.</summary>
    /// <param name="request">The prepared generation request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The generated temporary documents.</returns>
    Task<IReadOnlyList<GeneratedClassDocument>> GenerateAsync(
        GenerateClassDocumentsRequest request,
        CancellationToken cancellationToken);
}
