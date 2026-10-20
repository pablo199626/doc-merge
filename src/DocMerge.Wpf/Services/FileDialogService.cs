using System.IO;
using Microsoft.Win32;

namespace DocMerge.Wpf.Services;

/// <summary>Uses Windows native dialogs for input selection.</summary>
public sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc />
    public string? SelectFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <inheritdoc />
    public string? SelectDocx(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Documentos de Word (*.docx)|*.docx",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
