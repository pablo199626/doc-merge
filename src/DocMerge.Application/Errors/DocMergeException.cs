namespace DocMerge.Application.Errors;

public abstract class DocMergeException : Exception
{
    protected DocMergeException(string message)
        : base(message)
    {
    }

    protected DocMergeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
