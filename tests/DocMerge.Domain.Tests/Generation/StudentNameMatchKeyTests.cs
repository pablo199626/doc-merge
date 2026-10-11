using DocMerge.Domain.Generation;

namespace DocMerge.Domain.Tests.Generation;

public sealed class StudentNameMatchKeyTests
{
    [Theory]
    [InlineData("GARCÍA LÓPEZ, MARÍA", "garcia lopez maria")]
    [InlineData("  García   López,\tMaría  ", "garcia lopez maria")]
    [InlineData("Güemes, Ana", "guemes ana")]
    [InlineData("GU\u0308EMES, ANA", "guemes ana")]
    [InlineData("PEÑA, LUIS", "peña luis")]
    [InlineData("PEN\u0303A, LUIS", "peña luis")]
    [InlineData("Straße, Jörg", "strasse jorg")]
    public void Create_ToleratedDifferences_ReturnsCommonKey(string input, string expected)
    {
        var actual = StudentNameMatchKey.Create(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Peña, Luis", "Pena Luis")]
    [InlineData("García López, María", "María García López")]
    public void Create_DistinctNames_ReturnsDifferentKeys(string left, string right)
    {
        Assert.NotEqual(
            StudentNameMatchKey.Create(left),
            StudentNameMatchKey.Create(right));
    }

    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => StudentNameMatchKey.Create(null!));
    }
}
