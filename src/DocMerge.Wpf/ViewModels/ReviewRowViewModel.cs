using DocMerge.Wpf.Models;

namespace DocMerge.Wpf.ViewModels;

internal sealed class ReviewRowViewModel : ObservableObject
{
    private bool _isApplied;
    private bool _isAcknowledged;

    internal ReviewRowViewModel(
        int number,
        ReviewActionKind actionKind,
        ReviewRecord? source,
        ReviewRecord? destination,
        string explanation)
    {
        Number = number;
        ActionKind = actionKind;
        Source = source;
        Destination = destination;
        Explanation = explanation;
        _isApplied = actionKind is not ReviewActionKind.Conflict;
    }

    public int Number { get; }

    public ReviewActionKind ActionKind { get; }

    public ReviewRecord? Source { get; }

    public ReviewRecord? Destination { get; }

    public string Explanation { get; }

    public bool IsApplied
    {
        get => _isApplied;
        private set
        {
            if (!SetProperty(ref _isApplied, value))
            {
                return;
            }

            NotifyDerivedProperties();
        }
    }

    public bool IsAcknowledged
    {
        get => _isAcknowledged;
        private set
        {
            if (!SetProperty(ref _isAcknowledged, value))
            {
                return;
            }

            NotifyDerivedProperties();
        }
    }

    public ReviewRecord? Result => ActionKind switch
    {
        ReviewActionKind.Replace => IsApplied ? Source : Destination,
        ReviewActionKind.Add => IsApplied ? Source : null,
        ReviewActionKind.Delete => IsApplied ? null : Destination,
        ReviewActionKind.Conflict => Destination,
        _ => Destination,
    };

    public string StatusText => ActionKind switch
    {
        ReviewActionKind.Replace when IsApplied => "Sustituida desde origen",
        ReviewActionKind.Replace => "Se conserva el destino",
        ReviewActionKind.Add when IsApplied => "Incorporada",
        ReviewActionKind.Add => "Incorporación omitida",
        ReviewActionKind.Delete when IsApplied => "Baja propuesta",
        ReviewActionKind.Delete => "Se conserva el destino",
        ReviewActionKind.Conflict when IsAcknowledged => "Omisión revisada",
        ReviewActionKind.Conflict => "Revisión necesaria",
        _ => "Sin cambios",
    };

    public string Marker => ActionKind switch
    {
        ReviewActionKind.Replace => "~",
        ReviewActionKind.Add => "+",
        ReviewActionKind.Delete => "−",
        ReviewActionKind.Conflict => "!",
        _ => "✓",
    };

    public string ActionLabel => ActionKind switch
    {
        ReviewActionKind.Conflict when IsAcknowledged => "Marcar pendiente",
        ReviewActionKind.Conflict => "Reconocer omisión",
        _ when IsApplied => "Conservar destino",
        _ => "Restaurar propuesta",
    };

    public bool CanChange => ActionKind is not ReviewActionKind.Unchanged;

    public bool IsConflict => ActionKind == ReviewActionKind.Conflict;

    internal RowState CaptureState() => new(IsApplied, IsAcknowledged);

    internal void ToggleDecision()
    {
        if (ActionKind == ReviewActionKind.Conflict)
        {
            IsAcknowledged = !IsAcknowledged;
            return;
        }

        IsApplied = !IsApplied;
    }

    internal void RestoreState(RowState state)
    {
        IsApplied = state.IsApplied;
        IsAcknowledged = state.IsAcknowledged;
    }

    private void NotifyDerivedProperties()
    {
        NotifyPropertyChanged(nameof(Result));
        NotifyPropertyChanged(nameof(StatusText));
        NotifyPropertyChanged(nameof(ActionLabel));
    }

    internal sealed record RowState(bool IsApplied, bool IsAcknowledged);
}
