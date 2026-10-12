namespace DocMerge.Wpf.Services;

/// <summary>Controls and persists the desktop color theme.</summary>
public interface IThemeService
{
    /// <summary>Raised after the selected or effective theme changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the user's selected theme preference.</summary>
    ThemePreference Preference { get; }

    /// <summary>Gets whether the dark theme is active.</summary>
    bool IsDark { get; }

    /// <summary>Loads and applies the saved preference.</summary>
    void ApplySavedTheme();

    /// <summary>Applies and persists the selected theme preference.</summary>
    void SetPreference(ThemePreference preference);
}
