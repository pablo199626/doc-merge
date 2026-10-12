using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace DocMerge.Wpf.Services;

/// <summary>Observes the Windows application theme without exposing native details to ViewModels.</summary>
public sealed class WindowsSystemThemeService : ISystemThemeService, IDisposable
{
    /// <summary>Initializes and starts observing Windows preference changes.</summary>
    public WindowsSystemThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsDark => ApplicationThemeManager.GetSystemTheme() is
        SystemTheme.Dark or SystemTheme.HCBlack or SystemTheme.HC1;

    /// <inheritdoc />
    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color
            or UserPreferenceCategory.General
            or UserPreferenceCategory.VisualStyle)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
