using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Sorting;

/// <summary>Implements the certified historical name interpretation heuristic.</summary>
public sealed class LegacyNameInterpreter : INameInterpreter
{
    /// <inheritdoc/>
    public NameParts? Interpret(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        var collapsed = PythonUnicode.CollapseWhitespace(fullName);
        var parts = collapsed.Length == 0 ? [] : collapsed.Split(' ');
        if (parts.Length < 3)
        {
            return null;
        }

        return new NameParts(
            string.Join(' ', parts[..^2]),
            parts[^2],
            parts[^1]);
    }
}
