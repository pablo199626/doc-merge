namespace DocMerge.Wpf.Services;

/// <summary>Opens published folders using the Windows shell.</summary>
public interface ISystemFolderService
{
    /// <summary>Opens the supplied folder.</summary>
    void Open(string path);
}
