namespace DocMerge.Application.Generation;

/// <summary>Validates generated class documents before batch publication.</summary>
public interface IClassGenerationValidator
{
    /// <summary>Validates the complete temporary batch.</summary>
    /// <param name="request">The validation request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task representing validation.</returns>
    Task ValidateAsync(
        ValidateClassDocumentsRequest request,
        CancellationToken cancellationToken);
}
