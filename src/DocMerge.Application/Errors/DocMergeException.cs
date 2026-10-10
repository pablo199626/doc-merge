namespace DocMerge.Application.Errors;

/// <summary>Defines the base type for controlled application errors.</summary>
public abstract class DocMergeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DocMergeException"/> class.</summary>
    protected DocMergeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocMergeException"/> class.</summary>
    protected DocMergeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
