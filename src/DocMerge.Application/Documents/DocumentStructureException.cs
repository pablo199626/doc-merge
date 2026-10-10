using DocMerge.Application.Errors;

namespace DocMerge.Application.Documents;

/// <summary>Represents an unsupported, invalid, or unreadable document structure.</summary>
public sealed class DocumentStructureException : DocMergeException
{
    /// <summary>Initializes a new instance of the <see cref="DocumentStructureException"/> class.</summary>
    public DocumentStructureException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocumentStructureException"/> class.</summary>
    public DocumentStructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
