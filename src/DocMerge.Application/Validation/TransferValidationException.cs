using DocMerge.Application.Errors;

namespace DocMerge.Application.Validation;

/// <summary>Represents a generated transfer result that failed integrity validation.</summary>
public sealed class TransferValidationException : DocMergeException
{
    /// <summary>Initializes a new instance of the <see cref="TransferValidationException"/> class.</summary>
    public TransferValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransferValidationException"/> class.</summary>
    public TransferValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
