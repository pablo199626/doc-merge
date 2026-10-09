using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Tests.Identity;

public sealed class IdentityNormalizerTests
{
    [Theory]
    [InlineData("  FICCIO\u0301N  PEN\u0303A María ", "ficción peña maría")]
    [InlineData("Straße", "strasse")]
    [InlineData("ẞ", "ss")]
    [InlineData("İ", "i\u0307")]
    [InlineData("Σςσ", "σσσ")]
    [InlineData("ﬀ", "ff")]
    [InlineData("A\u00A0B\tC\r\nD", "a b c d")]
    public void Normalize_UnicodeInput_MatchesPythonCaseFold(string input, string expected)
    {
        var actual = IdentityNormalizer.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Peña María", "Pena María")]
    [InlineData("María", "Maria")]
    [InlineData("İ", "i")]
    [InlineData("ı", "i")]
    public void Normalize_DistinctIdentity_RemainsDistinct(string left, string right)
    {
        Assert.NotEqual(
            IdentityNormalizer.Normalize(left),
            IdentityNormalizer.Normalize(right));
    }
}
