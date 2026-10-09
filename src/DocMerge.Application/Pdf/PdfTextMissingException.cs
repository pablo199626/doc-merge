namespace DocMerge.Application.Pdf;

public sealed class PdfTextMissingException : PdfInputException
{
    public PdfTextMissingException(string message)
        : base(message)
    {
    }
}
