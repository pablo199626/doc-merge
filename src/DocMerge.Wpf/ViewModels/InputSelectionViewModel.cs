namespace DocMerge.Wpf.ViewModels;

/// <summary>Identifies a selectable generation input.</summary>
public enum InputKind
{
    /// <summary>The historical DOCX root.</summary>
    HistoricalDocuments,
    /// <summary>The class PDF root.</summary>
    Rosters,
    /// <summary>The generation template.</summary>
    Template,
}

/// <summary>Identifies the presentation state of one generation input.</summary>
public enum InputSelectionState
{
    /// <summary>No path has been selected.</summary>
    Empty,

    /// <summary>A path is selected but has not been analyzed.</summary>
    Selected,

    /// <summary>The selected path is being analyzed.</summary>
    Analyzing,

    /// <summary>The engine validated the selected path.</summary>
    Validated,

    /// <summary>The input requires user attention.</summary>
    Error,
}

/// <summary>Represents the state of one input card.</summary>
public sealed class InputSelectionViewModel : ViewModelBase
{
    private string? _path;
    private string _status = "Sin seleccionar";
    private string? _error;
    private int? _count;
    private bool _isBusy;
    private InputSelectionState _state;
    private string? _displayNameOverride;
    private bool _isBundled;

    /// <summary>Initializes an input card.</summary>
    public InputSelectionViewModel(InputKind kind, string title, string description)
    {
        Kind = kind;
        Title = title;
        Description = description;
    }

    /// <summary>Gets the input kind.</summary>
    public InputKind Kind { get; }

    /// <summary>Gets the card title.</summary>
    public string Title { get; }

    /// <summary>Gets the supporting description.</summary>
    public string Description { get; }

    /// <summary>Gets the selected path.</summary>
    public string? Path
    {
        get => _path;
        private set
        {
            if (SetProperty(ref _path, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(BrowseActionText));
            }
        }
    }

    /// <summary>Gets the readable selection status.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Gets the actionable validation error.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Gets the compatible file count discovered by the engine.</summary>
    public int? Count
    {
        get => _count;
        private set => SetProperty(ref _count, value);
    }

    /// <summary>Gets whether this input is being analyzed.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Gets whether a path has been selected.</summary>
    public bool HasSelection => !string.IsNullOrWhiteSpace(Path);

    /// <summary>Gets whether validation failed.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>Gets whether this selection is the official template included with DocMerge.</summary>
    public bool IsBundled => _isBundled;

    /// <summary>Gets whether the official template can replace the current external selection.</summary>
    public bool CanRestoreBundled => Kind == InputKind.Template && HasSelection && !IsBundled;

    /// <summary>Gets whether this selection may be removed instead of replaced.</summary>
    public bool CanRemoveSelection => Kind != InputKind.Template && HasSelection;

    /// <summary>Gets the explicit presentation state.</summary>
    public InputSelectionState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    /// <summary>Gets the selected file or directory name.</summary>
    public string DisplayName => Path is null
        ? _displayNameOverride ?? string.Empty
        : _displayNameOverride
            ?? GetDisplayName(Path);

    /// <summary>Gets a readable source without exposing an internal bundled-template path.</summary>
    public string DisplayLocation => IsBundled
        ? "Incluida con DocMerge"
        : Path
            ?? string.Empty;

    /// <summary>Gets the appropriate browse action label.</summary>
    public string BrowseActionText => HasSelection ? "Cambiar" : "Examinar…";

    /// <summary>Sets a path that passed immediate selection checks.</summary>
    public void Select(string path)
    {
        _displayNameOverride = null;
        _isBundled = false;
        Path = System.IO.Path.GetFullPath(path);
        Status = "Seleccionado · pendiente de analizar";
        Error = null;
        Count = null;
        State = InputSelectionState.Selected;
        NotifyTemplateSourceChanged();
    }

    /// <summary>Shows the included template while its private working copy is prepared.</summary>
    public void SetBundledPending(string displayName)
    {
        _displayNameOverride = displayName;
        _isBundled = true;
        Path = null;
        Status = "Incluida con DocMerge · preparando copia segura…";
        Error = null;
        State = InputSelectionState.Analyzing;
        NotifyTemplateSourceChanged();
    }

    /// <summary>Selects a verified private working copy of the included template.</summary>
    public void SelectBundled(string path, string displayName)
    {
        _displayNameOverride = displayName;
        _isBundled = true;
        Path = System.IO.Path.GetFullPath(path);
        Status = "Incluida con DocMerge · pendiente de analizar";
        Error = null;
        Count = null;
        State = InputSelectionState.Selected;
        NotifyTemplateSourceChanged();
    }

    /// <summary>Marks this input as being analyzed.</summary>
    public void StartAnalysis()
    {
        IsBusy = true;
        Status = "Analizando…";
        Error = null;
        Count = null;
        State = InputSelectionState.Analyzing;
    }

    /// <summary>Marks this input as validated by preparation.</summary>
    public void CompleteAnalysis(int? count)
    {
        IsBusy = false;
        Count = count;
        Status = count.HasValue
            ? $"Analizada · {count.Value} {(count.Value == 1 ? "documento" : "documentos")}"
            : IsBundled
                ? "Incluida con DocMerge · analizada"
                : "Plantilla externa · analizada";
        State = InputSelectionState.Validated;
    }

    /// <summary>Marks this input with an actionable error.</summary>
    public void SetError(string message)
    {
        IsBusy = false;
        Error = message;
        Status = "Requiere atención";
        State = InputSelectionState.Error;
    }

    /// <summary>Ends an interrupted analysis without losing a valid selection.</summary>
    public void StopAnalysis()
    {
        if (!IsBusy)
        {
            return;
        }

        IsBusy = false;
        Status = HasSelection ? "Seleccionado · pendiente de analizar" : "Sin seleccionar";
        State = HasSelection ? InputSelectionState.Selected : InputSelectionState.Empty;
    }

    /// <summary>Clears the selection.</summary>
    public void Clear()
    {
        _displayNameOverride = null;
        _isBundled = false;
        Path = null;
        Status = "Sin seleccionar";
        Error = null;
        Count = null;
        IsBusy = false;
        State = InputSelectionState.Empty;
        NotifyTemplateSourceChanged();
    }

    private void NotifyTemplateSourceChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DisplayLocation));
        OnPropertyChanged(nameof(IsBundled));
        OnPropertyChanged(nameof(CanRestoreBundled));
        OnPropertyChanged(nameof(CanRemoveSelection));
    }

    private static string GetDisplayName(string path)
    {
        var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var name = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
