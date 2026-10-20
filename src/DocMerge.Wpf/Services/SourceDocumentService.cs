using System.Diagnostics;
using System.IO;

namespace DocMerge.Wpf.Services;

/// <summary>Opens validated local review sources through the Windows shell.</summary>
public sealed class SourceDocumentService : ISourceDocumentService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx" };

    /// <inheritdoc />
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("La ruta del documento no es absoluta.");
        }

        if (!SupportedExtensions.Contains(Path.GetExtension(path)))
        {
            throw new InvalidOperationException("Solo se pueden abrir documentos PDF o DOCX.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("El documento original ya no está disponible.", path);
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
