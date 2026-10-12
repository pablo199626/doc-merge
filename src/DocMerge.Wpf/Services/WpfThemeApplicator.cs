using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace DocMerge.Wpf.Services;

/// <summary>Updates WPF UI and DocMerge resources through one application-level path.</summary>
public sealed class WpfThemeApplicator : IThemeApplicator
{
    private static readonly Color BrandAccent = Color.FromRgb(0x08, 0x66, 0xF5);
    private static readonly Uri LightTheme = new("Resources/LightTheme.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Resources/DarkTheme.xaml", UriKind.Relative);

    /// <inheritdoc />
    public void Apply(bool isDark)
    {
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
