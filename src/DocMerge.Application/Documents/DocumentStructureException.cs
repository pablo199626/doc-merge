using DocMerge.Application.Errors;

namespace DocMerge.Application.Documents;

public sealed class DocumentStructureException : DocMergeException
{
    public DocumentStructureException(string message)
        : base(message)
    {
    }

    public DocumentStructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
