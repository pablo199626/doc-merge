using DocMerge.Application.Errors;

namespace DocMerge.Application.Pdf;

public class PdfInputException : DocMergeException
{
    public PdfInputException(string message)
        : base(message)
    {
    }

    public PdfInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
