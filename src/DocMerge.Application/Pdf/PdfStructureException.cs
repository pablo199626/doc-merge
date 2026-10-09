namespace DocMerge.Application.Pdf;

public sealed class PdfStructureException : PdfInputException
{
    public PdfStructureException(string message)
        : base(message)
    {
    }
}
