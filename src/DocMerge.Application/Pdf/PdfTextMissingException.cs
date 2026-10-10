namespace DocMerge.Application.Pdf;

/// <summary>Represents a PDF that contains no usable extractable text.</summary>
public sealed class PdfTextMissingException : PdfInputException
{
    /// <summary>Initializes a new instance of the <see cref="PdfTextMissingException"/> class.</summary>
    public PdfTextMissingException(string message)
        : base(message)
    {
    }
}
