namespace DocMerge.Wpf.Services;

/// <summary>Abstracts native file and folder selection dialogs.</summary>
public interface IFileDialogService
{
    /// <summary>Selects an existing folder.</summary>
    string? SelectFolder(string title, string? initialDirectory = null);

    /// <summary>Selects one DOCX file.</summary>
    string? SelectDocx(string title, string? initialDirectory = null);
}
