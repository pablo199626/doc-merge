using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class InputSelectionViewModelTests
{
    [Fact]
    public void Select_ValidPath_MarksSelectionPendingAnalysis()
    {
        var input = new InputSelectionViewModel(
            InputKind.Template, "Plantilla", "Descripción", "icon");

        input.Select("template.docx");

        Assert.True(input.HasSelection);
        Assert.Equal("Seleccionado · pendiente de analizar", input.Status);
        Assert.False(input.HasError);
    }

    [Fact]
    public void SetError_InvalidSelection_ExposesTextAndNonColorState()
    {
        var input = new InputSelectionViewModel(
            InputKind.Rosters, "PDF", "Descripción", "icon");

        input.SetError("No contiene PDF compatibles.");

        Assert.True(input.HasError);
        Assert.Equal("Requiere atención", input.Status);
        Assert.Equal("No contiene PDF compatibles.", input.Error);
    }
}
