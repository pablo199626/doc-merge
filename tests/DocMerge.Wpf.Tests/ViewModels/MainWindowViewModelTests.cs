using DocMerge.Wpf.Models;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ChangeDecision_DeleteProposal_PreservesDestination()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Rows.Single(item => item.ActionKind == ReviewActionKind.Delete);

        // Act
        viewModel.ChangeDecisionCommand.Execute(row);

        // Assert
        Assert.False(row.IsApplied);
        Assert.Same(row.Destination, row.Result);
        Assert.Equal("Se conserva el destino", row.StatusText);
    }

    [Fact]
    public void Undo_ChangedDecision_RestoresSafeProposal()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Rows.Single(item => item.ActionKind == ReviewActionKind.Replace);
        viewModel.ChangeDecisionCommand.Execute(row);

        // Act
        viewModel.UndoCommand.Execute(null);

        // Assert
        Assert.True(row.IsApplied);
        Assert.Same(row.Source, row.Result);
        Assert.True(viewModel.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void ChangeDecision_Conflict_AcknowledgesOmissionWithoutApplyingSource()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Rows.Single(item => item.ActionKind == ReviewActionKind.Conflict);

        // Act
        viewModel.ChangeDecisionCommand.Execute(row);

        // Assert
        Assert.True(row.IsAcknowledged);
        Assert.Same(row.Destination, row.Result);
        Assert.Equal(0, viewModel.PendingIssues);
    }

    [Fact]
    public void RestoreSafeProposal_MultipleRejectedChanges_CreatesSingleUndoStep()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var changedRows = viewModel.Rows
            .Where(item => item.ActionKind is ReviewActionKind.Add or ReviewActionKind.Delete)
            .ToArray();
        foreach (var row in changedRows)
        {
            viewModel.ChangeDecisionCommand.Execute(row);
        }

        // Act
        viewModel.RestoreSafeProposalCommand.Execute(null);
        viewModel.UndoCommand.Execute(null);

        // Assert
        Assert.All(changedRows, row => Assert.False(row.IsApplied));
    }
}
