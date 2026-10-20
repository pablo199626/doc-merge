using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class InputSelectionViewModelTests
{
    [Fact]
    public void Select_ValidPath_MarksSelectionPendingAnalysis()
    {
        var input = new InputSelectionViewModel(
            InputKind.Template, "Plantilla", "Descripción");

        input.Select("template.docx");

        Assert.True(input.HasSelection);
        Assert.Equal("Seleccionado · pendiente de analizar", input.Status);
        Assert.False(input.HasError);
        Assert.Equal(InputSelectionState.Selected, input.State);
        Assert.Equal("template.docx", input.DisplayName);
    }

    [Fact]
    public void SetError_InvalidSelection_ExposesTextAndNonColorState()
    {
        var input = new InputSelectionViewModel(
            InputKind.Rosters, "PDF", "Descripción");

        input.SetError("No contiene PDF compatibles.");

        Assert.True(input.HasError);
        Assert.Equal("Requiere atención", input.Status);
        Assert.Equal("No contiene PDF compatibles.", input.Error);
        Assert.Equal(InputSelectionState.Error, input.State);
    }

    [Fact]
    public void StopAnalysis_InterruptedSelection_RestoresPendingState()
    {
        var input = new InputSelectionViewModel(
            InputKind.HistoricalDocuments, "Históricos", "Descripción");
        input.Select("history");
        input.StartAnalysis();

        input.StopAnalysis();

        Assert.False(input.IsBusy);
        Assert.Equal(InputSelectionState.Selected, input.State);
        Assert.Equal("Seleccionado · pendiente de analizar", input.Status);
    }

    [Fact]
    public void SelectBundled_OfficialTemplate_UsesCommercialProductName()
    {
        var input = new InputSelectionViewModel(
            InputKind.Template, "Plantilla", "Descripción");

        input.SelectBundled("template.docx", "Plantilla estándar de Talos");
        input.CompleteAnalysis(count: null);

        Assert.Equal("Incluida con Talos", input.DisplayLocation);
        Assert.Equal("Incluida con Talos · analizada", input.Status);
    }

    [Fact]
    public void ResetAnalysis_ValidatedBundledTemplate_PreservesSelectionAndMarksPending()
    {
        var input = new InputSelectionViewModel(
            InputKind.Template, "Plantilla", "Descripción");
        input.SelectBundled("template.docx", "Plantilla estándar de Talos");
        input.CompleteAnalysis(count: null);

        input.ResetAnalysis();

        Assert.True(input.HasSelection);
        Assert.True(input.IsBundled);
        Assert.Equal("Incluida con Talos", input.DisplayLocation);
        Assert.Equal("Incluida con Talos · pendiente de analizar", input.Status);
        Assert.Equal(InputSelectionState.Selected, input.State);
        Assert.Null(input.Count);
    }
}
