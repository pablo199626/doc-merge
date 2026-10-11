using System.Diagnostics;

namespace DocMerge.Wpf.Services;

/// <summary>Opens a folder through Windows Explorer.</summary>
public sealed class SystemFolderService : ISystemFolderService
{
    /// <inheritdoc />
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
