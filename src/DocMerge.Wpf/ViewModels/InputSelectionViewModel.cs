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

/// <summary>Represents the state of one input card.</summary>
public sealed class InputSelectionViewModel : ViewModelBase
{
    private string? _path;
    private string _status = "Sin seleccionar";
    private string? _error;
    private int? _count;
    private bool _isBusy;

    /// <summary>Initializes an input card.</summary>
    public InputSelectionViewModel(InputKind kind, string title, string description, string icon)
    {
        Kind = kind;
        Title = title;
        Description = description;
        Icon = icon;
    }

    /// <summary>Gets the input kind.</summary>
    public InputKind Kind { get; }

    /// <summary>Gets the card title.</summary>
    public string Title { get; }

    /// <summary>Gets the supporting description.</summary>
    public string Description { get; }

    /// <summary>Gets the symbolic icon.</summary>
    public string Icon { get; }

    /// <summary>Gets the selected path.</summary>
    public string? Path
    {
        get => _path;
        private set
        {
            if (SetProperty(ref _path, value))
            {
                OnPropertyChanged(nameof(HasSelection));
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

    /// <summary>Sets a path that passed immediate selection checks.</summary>
    public void Select(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Status = "Seleccionado · pendiente de analizar";
        Error = null;
        Count = null;
    }

    /// <summary>Marks this input as being analyzed.</summary>
    public void StartAnalysis()
    {
        IsBusy = true;
        Status = "Analizando…";
        Error = null;
        Count = null;
    }

    /// <summary>Marks this input as validated by preparation.</summary>
    public void CompleteAnalysis(int? count)
    {
        IsBusy = false;
        Count = count;
        Status = count.HasValue ? $"Comprobado · {count.Value} archivos" : "Comprobado";
    }

    /// <summary>Marks this input with an actionable error.</summary>
    public void SetError(string message)
    {
        IsBusy = false;
        Error = message;
        Status = "Requiere atención";
    }

    /// <summary>Clears the selection.</summary>
    public void Clear()
    {
        Path = null;
        Status = "Sin seleccionar";
        Error = null;
        Count = null;
        IsBusy = false;
    }
}
