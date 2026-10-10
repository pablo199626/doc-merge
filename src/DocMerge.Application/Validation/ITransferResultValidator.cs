namespace DocMerge.Application.Validation;

/// <summary>Validates a generated transfer result before publication.</summary>
public interface ITransferResultValidator
{
    /// <summary>Validates content, ordering, package integrity, and dependencies.</summary>
    /// <param name="request">The validation request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task ValidateAsync(
        TransferValidationRequest request,
        CancellationToken cancellationToken);
}
