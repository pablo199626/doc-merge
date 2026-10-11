using System.IO;
using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.Tests.Services;

public sealed class DesktopPreferencesServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"docmerge-preferences-{Guid.NewGuid():N}");

    [Fact]
    public void Load_MissingPreferences_UsesSystemAndCollapsedSidebar()
    {
        var service = new DesktopPreferencesService(_root);

        var preferences = service.Load();

        Assert.Equal(ThemePreference.System, preferences.ThemePreference);
        Assert.False(preferences.IsSidebarExpanded);
    }

    [Theory]
    [InlineData("light", ThemePreference.Light)]
    [InlineData("dark", ThemePreference.Dark)]
    public void Load_LegacyTheme_MigratesExplicitPreference(
        string legacyValue,
        ThemePreference expected)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "theme.txt"), legacyValue);
        var service = new DesktopPreferencesService(_root);

        var preferences = service.Load();

        Assert.Equal(expected, preferences.ThemePreference);
        Assert.True(File.Exists(Path.Combine(_root, "preferences.json")));
    }

    [Fact]
    public void SetSidebarExpanded_ExistingTheme_PreservesTheme()
    {
        var service = new DesktopPreferencesService(_root);
        service.SetThemePreference(ThemePreference.Dark);

        service.SetSidebarExpanded(isExpanded: true);

        var preferences = service.Load();
        Assert.Equal(ThemePreference.Dark, preferences.ThemePreference);
        Assert.True(preferences.IsSidebarExpanded);
    }

    [Fact]
    public void Load_InvalidPreferences_UsesSafeDefaults()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "preferences.json"), "{ invalid json }");
        var service = new DesktopPreferencesService(_root);

        var preferences = service.Load();

        Assert.Equal(DesktopPreferences.Default, preferences);
    }

    [Fact]
    public void Load_NewPreferencesAndLegacyTheme_NewPreferencesTakePriority()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "theme.txt"), "dark");
        var service = new DesktopPreferencesService(_root);
        service.SetThemePreference(ThemePreference.Light);

        var preferences = service.Load();

        Assert.Equal(ThemePreference.Light, preferences.ThemePreference);
    }

    [Fact]
    public void SetThemePreference_UnwritableLocation_DoesNotBlockSession()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(filePath, "content");
        var service = new DesktopPreferencesService(filePath);

        var error = Record.Exception(() => service.SetThemePreference(ThemePreference.Dark));

        Assert.Null(error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
