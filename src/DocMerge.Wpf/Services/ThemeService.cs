using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace DocMerge.Wpf.Services;

/// <summary>Applies centralized WPF theme dictionaries and stores one non-sensitive preference.</summary>
public sealed class ThemeService : IThemeService
{
    private const string ThemeFileName = "theme.txt";
    private static readonly Color BrandAccent = Color.FromRgb(0x08, 0x66, 0xF5);
    private static readonly Uri LightTheme = new("Resources/LightTheme.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Resources/DarkTheme.xaml", UriKind.Relative);

    /// <inheritdoc />
    public bool IsDark { get; private set; }

    /// <inheritdoc />
    public void ApplySavedTheme()
    {
        try
        {
            SetTheme(File.Exists(SettingsPath)
                && string.Equals(File.ReadAllText(SettingsPath).Trim(), "dark", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            Debug.WriteLine("No se pudo leer la preferencia de tema; se usará el tema claro.");
            SetTheme(isDark: false);
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine("No hay permisos para leer la preferencia de tema; se usará el tema claro.");
            SetTheme(isDark: false);
        }
    }

    /// <inheritdoc />
    public void SetDark(bool isDark)
    {
        SetTheme(isDark);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, isDark ? "dark" : "light");
        }
        catch (IOException)
        {
            Debug.WriteLine("No se pudo guardar la preferencia de tema.");
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine("No hay permisos para guardar la preferencia de tema.");
        }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DocMerge",
        ThemeFileName);

    private void SetTheme(bool isDark)
    {
        IsDark = isDark;
        var applicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(applicationTheme, updateAccent: false);
        ApplicationAccentColorManager.Apply(BrandAccent, applicationTheme);
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(item =>
            item.Source?.OriginalString.EndsWith("Theme.xaml", StringComparison.Ordinal) == true);
        if (current is not null)
        {
            dictionaries.Remove(current);
        }

        dictionaries.Add(new ResourceDictionary { Source = isDark ? DarkTheme : LightTheme });
    }
}
