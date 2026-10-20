namespace DocMerge.Wpf.Services;

/// <summary>Identifies the user's preferred desktop color theme.</summary>
public enum ThemePreference
{
    /// <summary>Follows the Windows application theme.</summary>
    System,

    /// <summary>Always uses the light theme.</summary>
    Light,

    /// <summary>Always uses the dark theme.</summary>
    Dark,
}

/// <summary>Stores non-sensitive presentation preferences.</summary>
public sealed record DesktopPreferences(
    ThemePreference ThemePreference,
    bool IsSidebarExpanded)
{
    /// <summary>Gets the preferences used on first launch.</summary>
    public static DesktopPreferences Default { get; } = new(ThemePreference.System, IsSidebarExpanded: false);
}
