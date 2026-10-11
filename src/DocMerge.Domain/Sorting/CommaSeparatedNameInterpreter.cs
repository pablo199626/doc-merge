using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Sorting;

/// <summary>Interprets names written as surnames followed by a comma and given names.</summary>
public sealed class CommaSeparatedNameInterpreter : INameInterpreter
{
    /// <inheritdoc/>
    public NameParts? Interpret(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        var sections = fullName.Split(',', 2, StringSplitOptions.TrimEntries);
        if (sections.Length != 2)
        {
            return null;
        }

        var surnames = IdentityNormalizer.NormalizeWhitespace(sections[0]).Split(' ');
        var givenName = IdentityNormalizer.NormalizeWhitespace(sections[1]);
        if (surnames.Length == 0 || givenName.Length == 0)
        {
            return null;
        }

        var secondSurname = surnames.Length == 1 ? string.Empty : string.Join(' ', surnames[1..]);
        return new NameParts(surnames[0], secondSurname, givenName);
    }
}
