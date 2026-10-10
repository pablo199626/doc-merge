using System.Text;

namespace DocMerge.Domain.Identity;

/// <summary>Provides the exact Unicode identity normalization used for matching.</summary>
public static class IdentityNormalizer
{
    /// <summary>Normalizes a name using Form C, Python whitespace rules, and full case folding.</summary>
    /// <param name="name">The identity text to normalize.</param>
    /// <returns>The normalized identity key.</returns>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Normalize(NormalizationForm.FormC);
        var collapsed = PythonUnicode.CollapseWhitespace(normalized);
        return PythonUnicode.CaseFold(collapsed);
    }

    /// <summary>Collapses Unicode whitespace according to the supported Python behavior.</summary>
    /// <param name="value">The text whose whitespace is normalized.</param>
    /// <returns>The whitespace-normalized text.</returns>
    public static string NormalizeWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return PythonUnicode.CollapseWhitespace(value);
    }
}
