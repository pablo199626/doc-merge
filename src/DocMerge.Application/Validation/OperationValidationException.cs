using DocMerge.Application.Errors;

namespace DocMerge.Application.Validation;

/// <summary>Represents an invalid operation request detected before external changes begin.</summary>
public sealed class OperationValidationException : DocMergeException
{
    /// <summary>Initializes a new instance of the <see cref="OperationValidationException"/> class.</summary>
    public OperationValidationException(string message)
        : base(message)
    {
    }
}
