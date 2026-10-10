namespace DocMerge.Domain.Sorting;

/// <summary>Represents the interpreted components of a person's name.</summary>
/// <param name="FirstSurname">The first surname.</param>
/// <param name="SecondSurname">The second surname.</param>
/// <param name="GivenName">The given-name component.</param>
public sealed record NameParts(
    string FirstSurname,
    string SecondSurname,
    string GivenName);
