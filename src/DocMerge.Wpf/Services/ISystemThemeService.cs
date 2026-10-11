namespace DocMerge.Wpf.Services;

/// <summary>Reports the current Windows application theme.</summary>
public interface ISystemThemeService
{
    /// <summary>Raised when the effective Windows application theme may have changed.</summary>
    event EventHandler? Changed;

    /// <summary>Gets whether Windows currently requests a dark application theme.</summary>
    bool IsDark { get; }
}
