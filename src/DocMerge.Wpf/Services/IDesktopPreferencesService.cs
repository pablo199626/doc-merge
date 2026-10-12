namespace DocMerge.Wpf.Services;

/// <summary>Loads and persists non-sensitive desktop presentation preferences.</summary>
public interface IDesktopPreferencesService
{
    /// <summary>Loads the current preferences or safe defaults.</summary>
    DesktopPreferences Load();

    /// <summary>Persists the selected theme while preserving other preferences.</summary>
    void SetThemePreference(ThemePreference preference);

    /// <summary>Persists the sidebar state while preserving other preferences.</summary>
    void SetSidebarExpanded(bool isExpanded);
}
