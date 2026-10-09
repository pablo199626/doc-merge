using System.Collections.Frozen;

namespace DocMerge.Domain.Sorting;

public static class SortingProfiles
{
    public const string DefaultIdentifier = "apellido-nombre-segundo-apellido";
    public const string GivenNameFirstIdentifier = "nombre-apellidos";

    private static readonly INameInterpreter LegacyInterpreter = new LegacyNameInterpreter();

    private static readonly SortingProfile[] OrderedProfiles =
    [
        new(DefaultIdentifier, LegacyInterpreter, parts =>
            new SortKey(parts.FirstSurname, parts.GivenName, parts.SecondSurname)),
        new(GivenNameFirstIdentifier, LegacyInterpreter, parts =>
            new SortKey(parts.GivenName, parts.FirstSurname, parts.SecondSurname)),
    ];

    private static readonly FrozenDictionary<string, SortingProfile> Profiles =
        OrderedProfiles.ToFrozenDictionary(profile => profile.Identifier, StringComparer.Ordinal);

    public static IReadOnlyList<string> Identifiers { get; } =
        OrderedProfiles.Select(profile => profile.Identifier).ToArray();

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
