namespace DocMerge.Wpf.Services;

/// <summary>Applies centralized WPF theme dictionaries and stores one non-sensitive preference.</summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly IDesktopPreferencesService _preferences;
    private readonly ISystemThemeService _systemTheme;
    private readonly IThemeApplicator _themeApplicator;
    private bool _isSubscribed;

    /// <summary>Initializes centralized theme coordination.</summary>
    public ThemeService(
        IDesktopPreferencesService preferences,
        ISystemThemeService systemTheme,
        IThemeApplicator themeApplicator)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _systemTheme = systemTheme ?? throw new ArgumentNullException(nameof(systemTheme));
        _themeApplicator = themeApplicator ?? throw new ArgumentNullException(nameof(themeApplicator));
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    /// <inheritdoc />
    public bool IsDark { get; private set; }

    /// <inheritdoc />
    public void ApplySavedTheme()
    {
        Preference = _preferences.Load().ThemePreference;
        if (!_isSubscribed)
        {
            _systemTheme.Changed += OnSystemThemeChanged;
            _isSubscribed = true;
        }

        ApplyEffectiveTheme();
    }

    /// <inheritdoc />
    public void SetPreference(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        if (Preference == preference)
        {
            return;
        }

        Preference = preference;
        _preferences.SetThemePreference(preference);
        ApplyEffectiveTheme();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _systemTheme.Changed -= OnSystemThemeChanged;
        _isSubscribed = false;
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (Preference != ThemePreference.System)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(ApplyEffectiveTheme);
            return;
        }

        ApplyEffectiveTheme();
    }

    private void ApplyEffectiveTheme()
    {
        SetTheme(Preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            ThemePreference.System => _systemTheme.IsDark,
            _ => false,
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetTheme(bool isDark)
    {
        IsDark = isDark;
        _themeApplicator.Apply(isDark);
    }
}
