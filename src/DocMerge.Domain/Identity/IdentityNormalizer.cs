using System.Text;

namespace DocMerge.Domain.Identity;

public static class IdentityNormalizer
{
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = name.Normalize(NormalizationForm.FormC);
        var collapsed = PythonUnicode.CollapseWhitespace(normalized);
        return PythonUnicode.CaseFold(collapsed);
    }

    public static string NormalizeWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return PythonUnicode.CollapseWhitespace(value);
    }
}
