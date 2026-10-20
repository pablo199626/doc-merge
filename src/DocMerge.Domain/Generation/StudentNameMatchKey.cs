using System.Globalization;
using System.Text;
using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Generation;

/// <summary>Creates the tolerant student-name key used by class generation.</summary>
public static class StudentNameMatchKey
{
    private const int CombiningTilde = 0x0303;

    /// <summary>Creates a comparison key without commas, letter case, or accents, while preserving ñ.</summary>
    /// <param name="name">The original student name.</param>
    /// <returns>The normalized comparison key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static string Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var withoutCommas = name.Replace(',', ' ');
        var folded = IdentityNormalizer.Normalize(withoutCommas);
        var withoutAccents = RemoveAccentsExceptEnye(folded);
        return IdentityNormalizer.NormalizeWhitespace(
            withoutAccents.Normalize(NormalizationForm.FormC));
    }

    private static string RemoveAccentsExceptEnye(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        Rune? starter = null;
        foreach (var rune in decomposed.EnumerateRunes())
        {
            if (IsCombiningMark(rune))
            {
                AppendPreservedTilde(result, starter, rune);
                continue;
            }

            result.Append(rune.ToString());
            starter = rune;
        }

        return result.ToString();
    }

    private static void AppendPreservedTilde(
        StringBuilder result,
        Rune? starter,
        Rune mark)
    {
        if (mark.Value == CombiningTilde && starter?.Value == 'n')
        {
            result.Append(mark.ToString());
        }
    }

    private static bool IsCombiningMark(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }
}
