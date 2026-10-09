namespace DocMerge.Application.Validation;

public interface ITransferResultValidator
{
    Task ValidateAsync(
        TransferValidationRequest request,
        CancellationToken cancellationToken);
}
