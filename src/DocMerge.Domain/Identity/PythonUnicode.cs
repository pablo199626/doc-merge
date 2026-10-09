using System.Text;

namespace DocMerge.Domain.Identity;

internal static class PythonUnicode
{
    internal static string CollapseWhitespace(string value)
    {
        var result = new StringBuilder(value.Length);
        var hasText = false;
        var pendingSpace = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (UnicodeData.Whitespace.Contains(rune.Value))
            {
                pendingSpace = hasText;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
            }

            result.Append(rune.ToString());
            hasText = true;
            pendingSpace = false;
        }

        return result.ToString();
    }

    internal static string CaseFold(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var folded = UnicodeData.CaseFolding.GetValueOrDefault(rune.Value);
            result.Append(folded ?? rune.ToString());
        }

        return result.ToString();
    }
}
