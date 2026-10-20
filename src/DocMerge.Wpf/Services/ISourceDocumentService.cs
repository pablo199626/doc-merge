namespace DocMerge.Wpf.Services;

/// <summary>Opens a local review source through its registered Windows application.</summary>
public interface ISourceDocumentService
{
    /// <summary>Opens a supported local source document.</summary>
    /// <param name="path">The absolute PDF or DOCX path.</param>
    void Open(string path);
}
