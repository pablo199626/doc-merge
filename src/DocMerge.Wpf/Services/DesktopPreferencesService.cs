using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocMerge.Wpf.Services;

/// <summary>Stores desktop presentation preferences in the user's local application data.</summary>
public sealed class DesktopPreferencesService : IDesktopPreferencesService
{
    private const string PreferencesFileName = "preferences.json";
    private const string LegacyThemeFileName = "theme.txt";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly string _settingsDirectory;

    /// <summary>Initializes preferences in the standard DocMerge settings directory.</summary>
    public DesktopPreferencesService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DocMerge"))
    {
    }

    /// <summary>Initializes preferences in an explicit directory for isolated hosts and tests.</summary>
    public DesktopPreferencesService(string settingsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        _settingsDirectory = Path.GetFullPath(settingsDirectory);
    }

    /// <inheritdoc />
    public DesktopPreferences Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                return ReadPreferences();
            }

            return MigrateLegacyPreference();
        }
        catch (IOException error)
        {
            Debug.WriteLine($"No se pudieron leer las preferencias: {error.Message}");
            return DesktopPreferences.Default;
        }
        catch (UnauthorizedAccessException error)
        {
            Debug.WriteLine($"No hay permisos para leer las preferencias: {error.Message}");
            return DesktopPreferences.Default;
        }
        catch (JsonException error)
        {
            Debug.WriteLine($"Las preferencias no son válidas: {error.Message}");
            return DesktopPreferences.Default;
        }
    }

    /// <inheritdoc />
    public void SetThemePreference(ThemePreference preference)
    {
        var current = Load();
        Save(current with { ThemePreference = preference });
    }

    /// <inheritdoc />
    public void SetSidebarExpanded(bool isExpanded)
    {
        var current = Load();
        Save(current with { IsSidebarExpanded = isExpanded });
    }

    private string PreferencesPath => Path.Combine(_settingsDirectory, PreferencesFileName);

    private string LegacyThemePath => Path.Combine(_settingsDirectory, LegacyThemeFileName);

    private DesktopPreferences ReadPreferences()
    {
        var preferences = JsonSerializer.Deserialize<DesktopPreferences>(
            File.ReadAllText(PreferencesPath),
            SerializerOptions);
        if (preferences is null || !Enum.IsDefined(preferences.ThemePreference))
        {
            return DesktopPreferences.Default;
        }

        return preferences;
    }

    private DesktopPreferences MigrateLegacyPreference()
    {
        if (!File.Exists(LegacyThemePath))
        {
            return DesktopPreferences.Default;
        }

        var legacyValue = File.ReadAllText(LegacyThemePath).Trim();
        var preference = legacyValue switch
        {
            "dark" => ThemePreference.Dark,
            "light" => ThemePreference.Light,
            _ => ThemePreference.System,
        };
        var migrated = DesktopPreferences.Default with { ThemePreference = preference };
        Save(migrated);
        return migrated;
    }

    private void Save(DesktopPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            File.WriteAllText(
                PreferencesPath,
                JsonSerializer.Serialize(preferences, SerializerOptions));
        }
        catch (IOException error)
        {
            Debug.WriteLine($"No se pudieron guardar las preferencias: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            Debug.WriteLine($"No hay permisos para guardar las preferencias: {error.Message}");
        }
    }
}
