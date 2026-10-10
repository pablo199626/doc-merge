namespace DocMerge.Application.Pdf;

/// <summary>Represents a PDF whose numbered person section is invalid or incomplete.</summary>
public sealed class PdfStructureException : PdfInputException
{
    /// <summary>Initializes a new instance of the <see cref="PdfStructureException"/> class.</summary>
    public PdfStructureException(string message)
        : base(message)
    {
    }
}
