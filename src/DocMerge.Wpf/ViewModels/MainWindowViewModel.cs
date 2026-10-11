using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMerge.Wpf.Commands;
using DocMerge.Wpf.Models;

namespace DocMerge.Wpf.ViewModels;

internal sealed class MainWindowViewModel : ObservableObject
{
    private readonly Stack<ReviewSnapshot> _undoHistory = new();
    private readonly Stack<ReviewSnapshot> _redoHistory = new();
    private readonly DelegateCommand _undoCommand;
    private readonly DelegateCommand _redoCommand;
    private ReviewRowViewModel? _selectedRow;
    private bool _compareWithSource = true;

    internal MainWindowViewModel()
    {
        Rows = new ObservableCollection<ReviewRowViewModel>(CreateDemoRows());
        _selectedRow = Rows.FirstOrDefault(row => row.ActionKind == ReviewActionKind.Replace);
        ChangeDecisionCommand = new DelegateCommand(ChangeDecision, CanChangeDecision);
        _undoCommand = new DelegateCommand(_ => Undo(), _ => _undoHistory.Count > 0);
        _redoCommand = new DelegateCommand(_ => Redo(), _ => _redoHistory.Count > 0);
        UndoCommand = _undoCommand;
        RedoCommand = _redoCommand;
        RestoreSafeProposalCommand = new DelegateCommand(_ => RestoreSafeProposal());
        NextIssueCommand = new DelegateCommand(_ => SelectNextIssue());
    }

    public ObservableCollection<ReviewRowViewModel> Rows { get; }

    public ReviewRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    public bool CompareWithSource
    {
        get => _compareWithSource;
        set
        {
            if (!SetProperty(ref _compareWithSource, value))
            {
                return;
            }

            NotifyPropertyChanged(nameof(CompactComparisonTitle));
        }
    }

    public string CompactComparisonTitle => CompareWithSource ? "Origen" : "Destino original";

    public int PendingIssues => Rows.Count(row =>
        row.ActionKind == ReviewActionKind.Conflict && !row.IsAcknowledged);

    public int ProposedChanges => Rows.Count(row =>
        row.ActionKind is ReviewActionKind.Replace or ReviewActionKind.Add or ReviewActionKind.Delete
        && row.IsApplied);

    public string ReviewSummary => $"{ProposedChanges} cambios en la propuesta · {PendingIssues} incidencias pendientes";

    public ICommand ChangeDecisionCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand RestoreSafeProposalCommand { get; }

    public ICommand NextIssueCommand { get; }

    private static IEnumerable<ReviewRowViewModel> CreateDemoRows()
    {
        yield return Row(1, ReviewActionKind.Unchanged, Record("Ana García López", "2.º A", "Sin incidencias"), Record("Ana García López", "2.º A", "Sin incidencias"), "Esta fila se conserva sin cambios.");
        yield return Row(2, ReviewActionKind.Unchanged, Record("Javier Romero Gil", "2.º A", "Sin incidencias"), Record("Javier Romero Gil", "2.º A", "Sin incidencias"), "Esta fila se conserva sin cambios.");
        yield return Row(3, ReviewActionKind.Replace, Record("Carlos Fernández Ruiz", "2.º B", "Adaptación"), Record("Carlos Fernández Ruiz", "2.º B", "—"), "La coincidencia es única. La fila completa del origen sustituye a la del destino.");
        yield return Row(4, ReviewActionKind.Unchanged, Record("Irene Vidal Cano", "2.º A", "Sin incidencias"), Record("Irene Vidal Cano", "2.º A", "Sin incidencias"), "Esta fila se conserva sin cambios.");
        yield return Row(5, ReviewActionKind.Add, Record("Lucía Martín Gómez", "2.º A", "Delegada"), null, "La persona aparece en el PDF y en el origen, pero no en el destino.");
        yield return Row(6, ReviewActionKind.Conflict, Record("Marta Sánchez Díaz", "2.º B", "Transporte"), Record("Marta Sánchez Díaz", "2.º A", "Comedor"), "La identidad coincide con dos filas del destino. Esta versión conserva el destino y exige reconocer la omisión.");
        yield return Row(7, ReviewActionKind.Unchanged, Record("Daniel Ortega Luna", "2.º B", "Sin incidencias"), Record("Daniel Ortega Luna", "2.º B", "Sin incidencias"), "Esta fila se conserva sin cambios.");
        yield return Row(8, ReviewActionKind.Delete, null, Record("Elena Navarro Pons", "2.º A", "Repetidora"), "La persona no aparece en el listado PDF, por lo que se propone su baja.");
        yield return Row(9, ReviewActionKind.Unchanged, Record("Noelia Prieto Sanz", "2.º A", "Sin incidencias"), Record("Noelia Prieto Sanz", "2.º A", "Sin incidencias"), "Esta fila se conserva sin cambios.");
    }

    private static ReviewRowViewModel Row(
        int number,
        ReviewActionKind kind,
        ReviewRecord? source,
        ReviewRecord? destination,
        string explanation) => new(number, kind, source, destination, explanation);

    private static ReviewRecord Record(string name, string group, string notes) => new(name, group, notes);

    private bool CanChangeDecision(object? parameter) =>
        parameter is ReviewRowViewModel { CanChange: true };

    private void ChangeDecision(object? parameter)
    {
        if (parameter is not ReviewRowViewModel row || !row.CanChange)
        {
            return;
        }

        PushUndoState();
        row.ToggleDecision();
        SelectedRow = row;
        NotifyReviewChanged();
    }

    private void RestoreSafeProposal()
    {
        var before = CaptureState();
        foreach (var row in Rows.Where(row => row.ActionKind is not ReviewActionKind.Conflict))
        {
            row.RestoreState(new ReviewRowViewModel.RowState(true, false));
        }

        if (before.Rows.SequenceEqual(CaptureState().Rows))
        {
            return;
        }

        _undoHistory.Push(before);
        _redoHistory.Clear();
        NotifyReviewChanged();
    }

    private void Undo()
    {
        if (!_undoHistory.TryPop(out var snapshot))
        {
            return;
        }

        _redoHistory.Push(CaptureState());
        RestoreState(snapshot);
    }

    private void Redo()
    {
        if (!_redoHistory.TryPop(out var snapshot))
        {
            return;
        }

        _undoHistory.Push(CaptureState());
        RestoreState(snapshot);
    }

    private void SelectNextIssue()
    {
        var issues = Rows.Where(row => row.ActionKind == ReviewActionKind.Conflict).ToArray();
        if (issues.Length == 0)
        {
            return;
        }

        var current = Array.IndexOf(issues, SelectedRow);
        SelectedRow = issues[(current + 1) % issues.Length];
    }

    private void PushUndoState()
    {
        _undoHistory.Push(CaptureState());
        _redoHistory.Clear();
    }

    private ReviewSnapshot CaptureState() => new(Rows.Select(row => row.CaptureState()).ToArray());

    private void RestoreState(ReviewSnapshot snapshot)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            Rows[index].RestoreState(snapshot.Rows[index]);
        }

        NotifyReviewChanged();
    }

    private void NotifyReviewChanged()
    {
        NotifyPropertyChanged(nameof(PendingIssues));
        NotifyPropertyChanged(nameof(ProposedChanges));
        NotifyPropertyChanged(nameof(ReviewSummary));
        _undoCommand.NotifyCanExecuteChanged();
        _redoCommand.NotifyCanExecuteChanged();
    }

    private sealed record ReviewSnapshot(IReadOnlyList<ReviewRowViewModel.RowState> Rows);
}
