namespace DocMerge.Wpf.Services;

/// <summary>Applies one effective theme to WPF UI and DocMerge resources.</summary>
public interface IThemeApplicator
{
    /// <summary>Applies the requested effective color theme.</summary>
    void Apply(bool isDark);
}
