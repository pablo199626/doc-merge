using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.Tests.Services;

public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplySavedTheme_SystemPreference_UsesCurrentWindowsTheme(bool systemIsDark)
    {
        var systemTheme = new StubSystemTheme { IsDark = systemIsDark };
        var applicator = new StubThemeApplicator();
        using var service = new ThemeService(
            new StubPreferences(DesktopPreferences.Default),
            systemTheme,
            applicator);

        service.ApplySavedTheme();

        Assert.Equal(systemIsDark, service.IsDark);
        Assert.Equal(systemIsDark, applicator.LastIsDark);
    }

    [Fact]
    public void SystemThemeChanged_SystemPreference_UpdatesEffectiveThemeAndNotifies()
    {
        var systemTheme = new StubSystemTheme();
        var applicator = new StubThemeApplicator();
        using var service = new ThemeService(
            new StubPreferences(DesktopPreferences.Default),
            systemTheme,
            applicator);
        service.ApplySavedTheme();
        var notificationCount = 0;
        service.Changed += (_, _) => notificationCount++;

        systemTheme.IsDark = true;
        systemTheme.RaiseChanged();

        Assert.True(service.IsDark);
        Assert.True(applicator.LastIsDark);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void SystemThemeChanged_ExplicitPreference_KeepsSelectedTheme()
    {
        var systemTheme = new StubSystemTheme();
        var applicator = new StubThemeApplicator();
        using var service = new ThemeService(
            new StubPreferences(new DesktopPreferences(ThemePreference.Light, false)),
            systemTheme,
            applicator);
        service.ApplySavedTheme();

        systemTheme.IsDark = true;
        systemTheme.RaiseChanged();

        Assert.False(service.IsDark);
        Assert.False(applicator.LastIsDark);
    }

    [Fact]
    public void Dispose_SystemThemeChanges_NoLongerUpdatesTheme()
    {
        var systemTheme = new StubSystemTheme();
        var applicator = new StubThemeApplicator();
        var service = new ThemeService(
            new StubPreferences(DesktopPreferences.Default),
            systemTheme,
            applicator);
        service.ApplySavedTheme();
        service.Dispose();

        systemTheme.IsDark = true;
        systemTheme.RaiseChanged();

        Assert.False(service.IsDark);
        Assert.False(applicator.LastIsDark);
    }

    private sealed class StubPreferences(DesktopPreferences preferences) : IDesktopPreferencesService
    {
        public DesktopPreferences Load() => preferences;
        public void SetThemePreference(ThemePreference preference)
        {
        }

        public void SetSidebarExpanded(bool isExpanded)
        {
        }
    }

    private sealed class StubSystemTheme : ISystemThemeService
    {
        public event EventHandler? Changed;
        public bool IsDark { get; set; }
        internal void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class StubThemeApplicator : IThemeApplicator
    {
        public bool LastIsDark { get; private set; }
        public void Apply(bool isDark) => LastIsDark = isDark;
    }
}
