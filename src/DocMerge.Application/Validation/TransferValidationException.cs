using DocMerge.Application.Errors;

namespace DocMerge.Application.Validation;

public sealed class TransferValidationException : DocMergeException
{
    public TransferValidationException(string message)
        : base(message)
    {
    }

    public TransferValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
