using DocMerge.Application.Generation;
using DocMerge.Application.Validation;
using DocMerge.Domain.Generation;

namespace DocMerge.Application.Tests.Generation;

public sealed class ClassHeaderValidationTests
{
    [Fact]
    public void CreateDefaultTitle_EsoIdentifier_ReturnsReadableTitle()
    {
        var result = ClassHeaderValidation.CreateDefaultTitle(
            new ClassIdentifier("ESO", 2, "C"));

        Assert.Equal("2º ESO C", result);
    }

    [Fact]
    public void Create_WhitespaceTutor_ReturnsEmptyTutor()
    {
        var result = ClassHeaderValidation.Create("2 ESO C", "  \u2003 ");

        Assert.Equal(string.Empty, result.TutorName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2 ESO C\nTutor")]
    public void Create_InvalidTitle_ThrowsValidationException(string title)
    {
        Assert.Throws<OperationValidationException>(() =>
            ClassHeaderValidation.Create(title, string.Empty));
    }

    [Fact]
    public void Create_SpecialCharacters_PreservesUserText()
    {
        var result = ClassHeaderValidation.Create("2.º ESO — C", "María Muñoz & Pérez");

        Assert.Equal("2.º ESO — C", result.DisplayTitle);
        Assert.Equal("María Muñoz & Pérez", result.TutorName);
    }

    [Fact]
    public void Create_TitleOverMaximumLength_ThrowsValidationException()
    {
        var title = new string('A', ClassHeaderValidation.MaximumTitleLength + 1);

        var error = Assert.Throws<OperationValidationException>(() =>
            ClassHeaderValidation.Create(title, string.Empty));

        Assert.Contains("60", error.Message, StringComparison.Ordinal);
    }
}
