namespace DocMerge.Wpf.Services;

/// <summary>Controls and persists the desktop color theme.</summary>
public interface IThemeService
{
    /// <summary>Gets whether the dark theme is active.</summary>
    bool IsDark { get; }

    /// <summary>Loads and applies the saved preference.</summary>
    void ApplySavedTheme();

    /// <summary>Applies and persists the selected theme.</summary>
    void SetDark(bool isDark);
}
