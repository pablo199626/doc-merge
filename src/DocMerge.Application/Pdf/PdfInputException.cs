using DocMerge.Application.Errors;

namespace DocMerge.Application.Pdf;

/// <summary>Represents an error that prevents a PDF list from being read safely.</summary>
public class PdfInputException : DocMergeException
{
    /// <summary>Initializes a new instance of the <see cref="PdfInputException"/> class.</summary>
    public PdfInputException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfInputException"/> class.</summary>
    public PdfInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
