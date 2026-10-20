using System.Collections.Frozen;

namespace DocMerge.Domain.Sorting;

/// <summary>Provides the registered sorting profiles exposed by the application.</summary>
public static class SortingProfiles
{
    /// <summary>The identifier of the surname-first profile.</summary>
    public const string DefaultIdentifier = "apellido-nombre-segundo-apellido";
    /// <summary>The identifier of the given-name-first profile.</summary>
    public const string GivenNameFirstIdentifier = "nombre-apellidos";
    /// <summary>The identifier of the comma-separated surname-first profile.</summary>
    public const string CommaSeparatedIdentifier = "apellidos-coma-nombre";

    private static readonly INameInterpreter LegacyInterpreter = new LegacyNameInterpreter();

    private static readonly SortingProfile[] OrderedProfiles =
    [
        new(DefaultIdentifier, LegacyInterpreter, parts =>
            new SortKey(parts.FirstSurname, parts.GivenName, parts.SecondSurname)),
        new(GivenNameFirstIdentifier, LegacyInterpreter, parts =>
            new SortKey(parts.GivenName, parts.FirstSurname, parts.SecondSurname)),
        new(CommaSeparatedIdentifier, new CommaSeparatedNameInterpreter(), parts =>
            new SortKey(parts.FirstSurname, parts.SecondSurname, parts.GivenName)),
    ];

    private static readonly FrozenDictionary<string, SortingProfile> Profiles =
        OrderedProfiles.ToFrozenDictionary(profile => profile.Identifier, StringComparer.Ordinal);

    /// <summary>Gets the profile identifiers in display order.</summary>
    public static IReadOnlyList<string> Identifiers { get; } =
        Array.AsReadOnly(OrderedProfiles.Select(profile => profile.Identifier).ToArray());

    /// <summary>Gets a registered profile by identifier.</summary>
    /// <param name="identifier">The exact profile identifier.</param>
    /// <returns>The registered profile.</returns>
    /// <exception cref="ArgumentException">The identifier is not registered.</exception>
    public static SortingProfile Get(string identifier)
    {
        if (Profiles.TryGetValue(identifier, out var profile))
        {
            return profile;
        }

        var available = string.Join(", ", Identifiers);
        throw new ArgumentException(
            $"Regla de ordenación no válida: {identifier}. Disponibles: {available}",
            nameof(identifier));
    }
}
