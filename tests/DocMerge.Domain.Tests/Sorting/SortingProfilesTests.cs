using DocMerge.Domain.Sorting;

namespace DocMerge.Domain.Tests.Sorting;

public sealed class SortingProfilesTests
{
    [Fact]
    public void CreateKey_SurnameProfile_MatchesPythonKey()
    {
        var key = SortingProfiles.Get(SortingProfiles.DefaultIdentifier)
            .CreateKey("Prueba Norte ANA");

        Assert.Equal(new SortKey("prueba", "ana", "norte"), key);
    }

    [Fact]
    public void CreateKey_GivenNameProfile_MatchesPythonKey()
    {
        var key = SortingProfiles.Get(SortingProfiles.GivenNameFirstIdentifier)
            .CreateKey("Prueba Norte ANA");

        Assert.Equal(new SortKey("ana", "prueba", "norte"), key);
    }

    [Fact]
    public void CreateKey_IncompleteName_SortsLast()
    {
        var key = SortingProfiles.Get(SortingProfiles.DefaultIdentifier)
            .CreateKey("Solo Nombre");

        Assert.Equal(SortKey.Invalid, key);
    }

    [Fact]
    public void Get_UnknownProfile_ReportsAvailableProfiles()
    {
        var error = Assert.Throws<ArgumentException>(() => SortingProfiles.Get("unknown-profile"));

        Assert.Contains("Disponibles", error.Message, StringComparison.Ordinal);
        Assert.Contains(SortingProfiles.DefaultIdentifier, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SortKeyComparer_SupplementaryCharacters_UsesCodePointOrder()
    {
        var basic = new SortKey("\uE000", string.Empty, string.Empty);
        var supplementary = new SortKey("\U00010000", string.Empty, string.Empty);

        Assert.True(SortKeyComparer.Instance.Compare(basic, supplementary) < 0);
    }
}
