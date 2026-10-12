using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using DocMerge.Application.Documents;
using DocMerge.Application.Errors;
using DocMerge.Application.Generation;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Sorting;
using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Coordinates the three-step class-generation desktop workflow.</summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const string StudentHeader = "Nombre y Apellidos";
    private const double CompactSidebarWidth = 64;
    private const double ExpandedSidebarWidth = 220;
    private const double SidebarOverlayThreshold = 900;
    private const double CompactReviewThreshold = 1160;
    private const string AllStatusesFilter = "Todos los estados";
    private const string RecoveredFilter = "Recuperados";
    private const string UniqueMatchFilter = "Recuperado";
    private const string EquivalentMatchesFilter = "Recuperado · históricos equivalentes";
    private const string MissingFilter = "Sin histórico";
    private const string UnresolvedFilter = "Conflictos";
    private const string ConflictingDataFilter = "Conflicto histórico";
    private const string AmbiguousIdentityFilter = "Identidad ambigua";
    private readonly ClassGenerationService _preparationService;
    private readonly ClassGenerationExecutionService _executionService;
    private readonly IFileDialogService _dialogs;
    private readonly IThemeService _themeService;
    private readonly IDesktopPreferencesService _preferences;
    private readonly ISystemFolderService _folderService;
    private readonly IBundledGenerationTemplateProvider? _bundledTemplateProvider;
    private PreparedClassGeneration? _prepared;
    private CancellationTokenSource? _operationCancellation;
    private int _inputRevision;
    private int _operationSequence;
    private int _currentStep = 1;
    private bool _isBusy;
    private bool _isDark;
    private ThemePreference _selectedThemePreference;
    private bool _isSidebarExpanded;
    private double _windowWidth = 1280;
    private string? _generalError;
    private string _progressText = string.Empty;
    private ClassReviewViewModel? _selectedClass;
    private string _searchText = string.Empty;
    private string _selectedFilter = AllStatusesFilter;
    private string? _outputParent;
    private string _outputFolderName = "Clases generadas";
    private string? _publishedDirectory;
    private string? _finalStatus;
    private bool _isBundledTemplateInitialized;

    /// <summary>Initializes the desktop workflow.</summary>
    public MainWindowViewModel(
        ClassGenerationService preparationService,
        ClassGenerationExecutionService executionService,
        IFileDialogService dialogs,
        IThemeService themeService,
        IDesktopPreferencesService preferences,
        ISystemFolderService folderService,
        IBundledGenerationTemplateProvider? bundledTemplateProvider = null)
    {
        _preparationService = preparationService ?? throw new ArgumentNullException(nameof(preparationService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _bundledTemplateProvider = bundledTemplateProvider;
        _isDark = themeService.IsDark;
        _selectedThemePreference = themeService.Preference;
        _isSidebarExpanded = preferences.Load().IsSidebarExpanded;
        themeService.Changed += OnThemeChanged;

        HistoricalInput = new InputSelectionViewModel(
            InputKind.HistoricalDocuments,
            "Documentos históricos (DOCX)",
            "Carpeta con la base histórica global");
        RosterInput = new InputSelectionViewModel(
            InputKind.Rosters,
            "Listados de alumnos (PDF)",
            "Carpeta con los PDF que determinan las clases");
        TemplateInput = new InputSelectionViewModel(
            InputKind.Template,
            "Plantilla (DOCX)",
            "Plantilla estándar de DocMerge");
        if (_bundledTemplateProvider is not null)
        {
            TemplateInput.SetBundledPending("Plantilla estándar de DocMerge");
        }

        BrowseInputCommand = new RelayCommand(BrowseInput, _ => !IsBusy);
        RemoveInputCommand = new RelayCommand(RemoveInput, _ => !IsBusy);
        RestoreIncludedTemplateCommand = new AsyncRelayCommand(
            RestoreIncludedTemplateAsync,
            () => !IsBusy && TemplateInput.CanRestoreBundled,
            HandleUnexpectedError);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze, HandleUnexpectedError);
        CancelCommand = new RelayCommand(_ => CancelOperation(), _ => IsBusy);
        BackToFilesCommand = new RelayCommand(_ => CurrentStep = 1, _ => !IsBusy);
        ContinueToGenerationCommand = new RelayCommand(
            _ => CurrentStep = 3,
            _ => CanContinueToGeneration);
        BackToReviewCommand = new RelayCommand(_ => CurrentStep = 2, _ => !IsBusy);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput(), _ => !IsBusy);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, CanGenerate, HandleUnexpectedError);
        OpenOutputCommand = new RelayCommand(_ => OpenOutput(), _ => PublishedDirectory is not null);
        ShowIssueCommand = new RelayCommand(ShowIssue);
        ShowAllIssuesCommand = new RelayCommand(_ => ShowAllIssues(), _ => Issues.Count > 0);
        ShowIssueGroupCommand = new RelayCommand(ShowIssueGroup);
        ShowStudentCommand = new RelayCommand(ShowStudent);
        FilterByStatusCommand = new RelayCommand(FilterByStatus);
        ClearReviewFiltersCommand = new RelayCommand(_ => ClearReviewFilters(), _ => HasActiveReviewFilters);
        SetThemeCommand = new RelayCommand(SetTheme);
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
        CloseSidebarOverlayCommand = new RelayCommand(_ => IsSidebarExpanded = false);
    }

    /// <summary>Raised when a temporary read-only detail dialog should be displayed.</summary>
    public event EventHandler<DetailRequestedEventArgs>? DetailRequested;

    /// <summary>Gets the historical input card.</summary>
    public InputSelectionViewModel HistoricalInput { get; }

    /// <summary>Gets the roster input card.</summary>
    public InputSelectionViewModel RosterInput { get; }

    /// <summary>Gets the template input card.</summary>
    public InputSelectionViewModel TemplateInput { get; }

    /// <summary>Gets class review items.</summary>
    public ObservableCollection<ClassReviewViewModel> Classes { get; } = [];

    /// <summary>Gets students currently shown by search and status filters.</summary>
    public ObservableCollection<StudentReviewViewModel> VisibleStudents { get; } = [];

    /// <summary>Gets all structured issues in the prepared batch.</summary>
    public ObservableCollection<ReviewIssueViewModel> Issues { get; } = [];

    /// <summary>Gets document-header values for every class in the reviewed batch.</summary>
    public ObservableCollection<ClassHeaderViewModel> ClassHeaders { get; } = [];

    /// <summary>Gets available review filters.</summary>
    public IReadOnlyList<string> StatusFilters { get; } =
    [
        AllStatusesFilter,
        RecoveredFilter,
        UniqueMatchFilter,
        EquivalentMatchesFilter,
        MissingFilter,
        UnresolvedFilter,
        ConflictingDataFilter,
        AmbiguousIdentityFilter,
    ];

    /// <summary>Gets the command that opens an input selector.</summary>
    public RelayCommand BrowseInputCommand { get; }

    /// <summary>Gets the command that removes one input.</summary>
    public RelayCommand RemoveInputCommand { get; }

    /// <summary>Gets the command that restores the official template.</summary>
    public AsyncRelayCommand RestoreIncludedTemplateCommand { get; }

    /// <summary>Gets the command that prepares a review plan.</summary>
    public AsyncRelayCommand AnalyzeCommand { get; }

    /// <summary>Gets the command that requests safe cancellation.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Gets the command that returns to input selection.</summary>
    public RelayCommand BackToFilesCommand { get; }

    /// <summary>Gets the command that advances to final generation.</summary>
    public RelayCommand ContinueToGenerationCommand { get; }

    /// <summary>Gets the command that returns to review.</summary>
    public RelayCommand BackToReviewCommand { get; }

    /// <summary>Gets the command that selects the output parent.</summary>
    public RelayCommand BrowseOutputCommand { get; }

    /// <summary>Gets the command that writes the reviewed batch.</summary>
    public AsyncRelayCommand GenerateCommand { get; }

    /// <summary>Gets the command that opens the published folder.</summary>
    public RelayCommand OpenOutputCommand { get; }

    /// <summary>Gets the command that shows issue detail.</summary>
    public RelayCommand ShowIssueCommand { get; }

    /// <summary>Gets the command that shows all batch issues.</summary>
    public RelayCommand ShowAllIssuesCommand { get; }

    /// <summary>Gets the command that shows one traceable class-issue subset.</summary>
    public RelayCommand ShowIssueGroupCommand { get; }

    /// <summary>Gets the command that shows student detail.</summary>
    public RelayCommand ShowStudentCommand { get; }

    /// <summary>Gets the command that activates a review-only status filter.</summary>
    public RelayCommand FilterByStatusCommand { get; }

    /// <summary>Gets the command that clears search and status filters.</summary>
    public RelayCommand ClearReviewFiltersCommand { get; }

    /// <summary>Gets the command that selects a color theme.</summary>
    public RelayCommand SetThemeCommand { get; }

    /// <summary>Gets the command that expands or collapses the sidebar.</summary>
    public RelayCommand ToggleSidebarCommand { get; }

    /// <summary>Gets the command that closes an overlaid sidebar.</summary>
    public RelayCommand CloseSidebarOverlayCommand { get; }

    /// <summary>Gets the available appearance choices.</summary>
    public IReadOnlyList<ThemeOptionViewModel> ThemeOptions { get; } =
    [
        new(ThemePreference.System, "Sistema"),
        new(ThemePreference.Light, "Claro"),
        new(ThemePreference.Dark, "Oscuro"),
    ];

    /// <summary>Gets or sets the active workflow step.</summary>
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (!SetProperty(ref _currentStep, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSelectionStep));
            OnPropertyChanged(nameof(IsReviewStep));
            OnPropertyChanged(nameof(IsGenerationStep));
            OnPropertyChanged(nameof(StepOneIndicator));
            OnPropertyChanged(nameof(StepTwoIndicator));
            OnPropertyChanged(nameof(StepThreeIndicator));
            OnPropertyChanged(nameof(StepOneState));
            OnPropertyChanged(nameof(StepTwoState));
            OnPropertyChanged(nameof(StepThreeState));
        }
    }

    /// <summary>Gets whether selection is visible.</summary>
    public bool IsSelectionStep => CurrentStep == 1;

    /// <summary>Gets whether review is visible.</summary>
    public bool IsReviewStep => CurrentStep == 2;

    /// <summary>Gets whether final generation is visible.</summary>
    public bool IsGenerationStep => CurrentStep == 3;

    /// <summary>Gets the first step number or completion mark.</summary>
    public string StepOneIndicator => CurrentStep == 1 ? "1" : "✓";

    /// <summary>Gets the second step number or completion mark.</summary>
    public string StepTwoIndicator => CurrentStep < 3 ? "2" : "✓";

    /// <summary>Gets the third step number or completion mark.</summary>
    public string StepThreeIndicator => PublishedDirectory is null ? "3" : "✓";

    /// <summary>Gets the visual state of input selection.</summary>
    public WorkflowStepState StepOneState => CurrentStep == 1
        ? WorkflowStepState.Current
        : WorkflowStepState.Completed;

    /// <summary>Gets the visual state of review.</summary>
    public WorkflowStepState StepTwoState => CurrentStep switch
    {
        < 2 => WorkflowStepState.Pending,
        2 => WorkflowStepState.Current,
        _ => WorkflowStepState.Completed,
    };

    /// <summary>Gets the visual state of document generation.</summary>
    public WorkflowStepState StepThreeState => PublishedDirectory is not null
        ? WorkflowStepState.Completed
        : CurrentStep == 3
            ? WorkflowStepState.Current
            : WorkflowStepState.Pending;

    /// <summary>Gets whether a long operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets whether the effective theme is dark.</summary>
    public bool IsDark
    {
        get => _isDark;
        private set => SetProperty(ref _isDark, value);
    }

    /// <summary>Gets or sets the selected color-theme preference.</summary>
    public ThemePreference SelectedThemePreference
    {
        get => _selectedThemePreference;
        set
        {
            if (!SetProperty(ref _selectedThemePreference, value))
            {
                return;
            }

            _themeService.SetPreference(value);
            OnPropertyChanged(nameof(ThemeSummary));
            NotifyThemeSelectionChanged();
        }
    }

    /// <summary>Gets the selected preference and effective system result.</summary>
    public string ThemeSummary => SelectedThemePreference == ThemePreference.System
        ? $"Sistema · {(IsDark ? "oscuro" : "claro")}"
        : ThemeOptions.First(option => option.Value == SelectedThemePreference).Label;

    /// <summary>Gets whether the system theme option is selected.</summary>
    public bool IsSystemThemeSelected => SelectedThemePreference == ThemePreference.System;

    /// <summary>Gets whether the light theme option is selected.</summary>
    public bool IsLightThemeSelected => SelectedThemePreference == ThemePreference.Light;

    /// <summary>Gets whether the dark theme option is selected.</summary>
    public bool IsDarkThemeSelected => SelectedThemePreference == ThemePreference.Dark;

    /// <summary>Gets or sets whether the sidebar is explicitly expanded.</summary>
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (!SetProperty(ref _isSidebarExpanded, value))
            {
                return;
            }

            _preferences.SetSidebarExpanded(value);
            NotifyLayoutChanged();
        }
    }

    /// <summary>Gets the sidebar's visual width.</summary>
    public double SidebarWidth => IsSidebarExpanded ? ExpandedSidebarWidth : CompactSidebarWidth;

    /// <summary>Gets the width reserved by the shell layout.</summary>
    public double SidebarColumnWidth => IsSidebarOverlayMode ? CompactSidebarWidth : SidebarWidth;

    /// <summary>Gets whether the sidebar is overlaid instead of consuming its expanded width.</summary>
    public bool IsSidebarOverlayMode => _windowWidth < SidebarOverlayThreshold;

    /// <summary>Gets whether the sidebar currently blocks the compact content area.</summary>
    public bool IsSidebarOverlayVisible => IsSidebarExpanded && IsSidebarOverlayMode;

    /// <summary>Gets whether review uses its vertically stacked class selector.</summary>
    public bool IsCompactReviewLayout => _windowWidth - SidebarColumnWidth < CompactReviewThreshold;

    /// <summary>Gets the current general error.</summary>
    public string? GeneralError
    {
        get => _generalError;
        private set
        {
            if (SetProperty(ref _generalError, value))
            {
                OnPropertyChanged(nameof(HasGeneralError));
            }
        }
    }

    /// <summary>Gets whether a general error is present.</summary>
    public bool HasGeneralError => !string.IsNullOrWhiteSpace(GeneralError);

    /// <summary>Gets current operation progress without inventing a percentage.</summary>
    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    /// <summary>Gets or sets the selected class.</summary>
    public ClassReviewViewModel? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
            {
                ApplyStudentFilter();
                OnPropertyChanged(nameof(VisibleStudentSummary));
            }
        }
    }

    /// <summary>Gets or sets the student search text.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyStudentFilter();
                NotifyReviewFilterChanged();
            }
        }
    }

    /// <summary>Gets or sets the selected review-only status filter.</summary>
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyStudentFilter();
                NotifyReviewFilterChanged();
            }
        }
    }

    /// <summary>Gets the filtered and total student count.</summary>
    public string VisibleStudentSummary => SelectedClass is null
        ? "Sin clase seleccionada"
        : $"Mostrando {VisibleStudents.Count} de {SelectedClass.StudentCount} alumnos";

    /// <summary>Gets whether the active filters contain at least one student.</summary>
    public bool HasVisibleStudents => VisibleStudents.Count > 0;

    /// <summary>Gets whether search or status filtering is active.</summary>
    public bool HasActiveReviewFilters => !string.IsNullOrWhiteSpace(SearchText)
        || SelectedFilter != AllStatusesFilter;

    /// <summary>Gets a readable description of active review filters.</summary>
    public string ActiveFilterSummary => (string.IsNullOrWhiteSpace(SearchText), SelectedFilter) switch
    {
        (true, AllStatusesFilter) => string.Empty,
        (false, AllStatusesFilter) => "Búsqueda activa",
        (true, _) => $"Filtro: {SelectedFilter}",
        _ => $"Búsqueda · Filtro: {SelectedFilter}",
    };

    /// <summary>Gets the complete batch summary.</summary>
    public string BatchSummary => _prepared is null
        ? string.Empty
        : $"{CountText(_prepared.Plan.Classes.Count, "documento", "documentos")} · "
            + $"{CountText(_prepared.Plan.Classes.Sum(item => item.Rows.Count), "alumno", "alumnos")} · "
            + CountText(_prepared.Plan.Issues.Count, "incidencia", "incidencias");

    /// <summary>Gets whether blocking issues prevent final generation.</summary>
    public bool HasBlockingIssues => _prepared?.Plan.HasBlockingIssues == true;

    /// <summary>Gets whether the selected template supports title and tutor fields.</summary>
    public bool IsHeaderCustomizationAvailable => _prepared?.Template.SupportsDocumentHeader == true;

    /// <summary>Gets guidance for the selected template's header support.</summary>
    public string HeaderCustomizationMessage => IsHeaderCustomizationAvailable
        ? "El tutor o tutora es opcional. Si se deja vacío, se conservará el espacio previsto en la plantilla."
        : "Esta plantilla es compatible con la generación, pero no admite personalizar la cabecera.";

    /// <summary>Gets whether any editable document header is invalid.</summary>
    public bool HasHeaderErrors => ClassHeaders.Any(item => item.HasErrors);

    /// <summary>Gets or sets the existing parent folder for output.</summary>
    public string? OutputParent
    {
        get => _outputParent;
        private set
        {
            if (SetProperty(ref _outputParent, value))
            {
                OnPropertyChanged(nameof(OutputDirectory));
                OnPropertyChanged(nameof(OutputError));
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets or sets the name of the new output folder.</summary>
    public string OutputFolderName
    {
        get => _outputFolderName;
        set
        {
            if (SetProperty(ref _outputFolderName, value))
            {
                OnPropertyChanged(nameof(OutputDirectory));
                OnPropertyChanged(nameof(OutputError));
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets the composed output directory.</summary>
    public string? OutputDirectory => string.IsNullOrWhiteSpace(OutputParent)
        || string.IsNullOrWhiteSpace(OutputFolderName)
        ? null
        : Path.Combine(OutputParent, OutputFolderName.Trim());

    /// <summary>Gets an actionable output-path validation message.</summary>
    public string? OutputError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(OutputParent))
            {
                return "Selecciona la carpeta contenedora.";
            }

            if (!IsValidFolderName(OutputFolderName))
            {
                return "Escribe solo el nombre de una carpeta nueva, sin barras ni caracteres no válidos.";
            }

            return OutputDirectory is not null
                && (Directory.Exists(OutputDirectory) || File.Exists(OutputDirectory))
                ? "La carpeta de salida debe ser nueva; esa ruta ya existe."
                : null;
        }
    }

    /// <summary>Gets the directory published by the last successful execution.</summary>
    public string? PublishedDirectory
    {
        get => _publishedDirectory;
        private set
        {
            if (SetProperty(ref _publishedDirectory, value))
            {
                OpenOutputCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(StepThreeIndicator));
                OnPropertyChanged(nameof(StepThreeState));
            }
        }
    }

    /// <summary>Gets the final success, cancellation, or failure text.</summary>
    public string? FinalStatus
    {
        get => _finalStatus;
        private set => SetProperty(ref _finalStatus, value);
    }

    /// <summary>Gets the assembly product version.</summary>
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.1.0";

    /// <summary>Gets whether review can advance to final generation.</summary>
    public bool CanContinueToGeneration => _prepared is not null && !HasBlockingIssues && !IsBusy;

    /// <summary>Prepares the included template without exposing its internal path.</summary>
    public async Task InitializeAsync()
    {
        if (_bundledTemplateProvider is null || _isBundledTemplateInitialized)
        {
            return;
        }

        _isBundledTemplateInitialized = true;
        try
        {
            var template = await _bundledTemplateProvider.GetAsync(CancellationToken.None);
            TemplateInput.SelectBundled(template.Path, template.DisplayName);
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException
            or DocMergeException)
        {
            TemplateInput.SetError($"No se pudo preparar la plantilla incluida: {error.Message}");
        }

        RefreshCommands();
    }

    /// <summary>Accepts a path from drag and drop after validating its immediate type.</summary>
    public void SelectDroppedPath(InputKind kind, string path)
    {
        if (!IsBusy)
        {
            SelectInput(kind, path);
        }
    }

    /// <summary>Shows an inline drag-and-drop error without discarding an existing selection.</summary>
    public void RejectDroppedPath(InputKind kind, string message)
    {
        if (!IsBusy)
        {
            SetSelectionError(GetInput(kind), message);
        }
    }

    /// <summary>Updates adaptive presentation state from the current window width.</summary>
    public void UpdateWindowWidth(double width)
    {
        if (Math.Abs(_windowWidth - width) < 0.5)
        {
            return;
        }

        _windowWidth = width;
        NotifyLayoutChanged();
    }

    /// <summary>Requests cancellation and returns a task that completes when the active operation finishes.</summary>
    public void CancelOperation()
    {
        ProgressText = "Cancelando de forma segura…";
        _operationCancellation?.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _themeService.Changed -= OnThemeChanged;
        ClearClassHeaders();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void BrowseInput(object? parameter)
    {
        if (parameter is not InputSelectionViewModel input)
        {
            return;
        }

        var selected = input.Kind == InputKind.Template
            ? _dialogs.SelectDocx("Selecciona la plantilla DOCX", InitialDirectory(input.Path))
            : _dialogs.SelectFolder($"Selecciona {input.Title.ToLowerInvariant()}", input.Path);
        if (selected is not null)
        {
            SelectInput(input.Kind, selected);
        }
    }

    private void SelectInput(InputKind kind, string path)
    {
        var input = GetInput(kind);
        var error = ValidateImmediate(kind, path);
        if (error is not null)
        {
            SetSelectionError(input, error);
            return;
        }

        if (string.Equals(input.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        input.Select(path);
        InvalidatePreparedReview();
    }

    private async Task RestoreIncludedTemplateAsync()
    {
        if (_bundledTemplateProvider is null)
        {
            return;
        }

        var template = await _bundledTemplateProvider.GetAsync(CancellationToken.None);
        if (TemplateInput.IsBundled
            && string.Equals(TemplateInput.Path, template.Path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TemplateInput.SelectBundled(template.Path, template.DisplayName);
        InvalidatePreparedReview();
    }

    private void RemoveInput(object? parameter)
    {
        if (parameter is InputSelectionViewModel { Kind: not InputKind.Template } input)
        {
            input.Clear();
            InvalidatePreparedReview();
        }
    }

    private async Task AnalyzeAsync()
    {
        var revision = _inputRevision;
        GeneralError = null;
        StartInputAnalysis();
        IsBusy = true;
        _operationCancellation = new CancellationTokenSource();
        var operation = ++_operationSequence;
        var progress = CreateProgress(operation);
        var request = new ClassGenerationRequest(
            HistoricalInput.Path!, RosterInput.Path!, TemplateInput.Path!, StudentHeader);
        try
        {
            var prepared = await Task.Run(
                () => _preparationService.PrepareAsync(
                    request, _operationCancellation.Token, progress),
                _operationCancellation.Token);
            if (revision != _inputRevision)
            {
                return;
            }

            if (TemplateInput.IsBundled && !prepared.Template.SupportsDocumentHeader)
            {
                TemplateInput.SetError(
                    "La plantilla estándar incluida no contiene los campos de clase y tutor requeridos.");
                GeneralError = "La plantilla incluida es incompatible. No se ha creado un plan de revisión.";
                return;
            }

            _prepared = prepared;
            CompleteInputAnalysis(prepared);
            BuildReview(prepared);
            CurrentStep = 2;
        }
        catch (OperationCanceledException)
        {
            ResetInputAfterCancellation();
            GeneralError = "Análisis cancelado. No se ha creado ningún plan de revisión.";
        }
        catch (GenerationInputException error)
        {
            SetInputError(error);
        }
        catch (DocMergeException error)
        {
            GeneralError = error.Message;
        }
        finally
        {
            StopInputAnalysis();
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _operationSequence++;
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    private async Task GenerateAsync()
    {
        if (_prepared is null || OutputDirectory is null)
        {
            return;
        }

        GeneralError = null;
        FinalStatus = null;
        PublishedDirectory = null;
        IsBusy = true;
        _operationCancellation = new CancellationTokenSource();
        var operation = ++_operationSequence;
        var progress = CreateProgress(operation);
        try
        {
            var request = new ClassGenerationExecutionRequest(
                _prepared,
                OutputDirectory,
                SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier),
                CreateOutputOptions());
            var result = await Task.Run(
                () => _executionService.ExecuteAsync(
                    request, _operationCancellation.Token, progress),
                _operationCancellation.Token);
            PublishedDirectory = result.OutputDirectory;
            FinalStatus = $"Generación completada: {result.Documents.Count} documentos publicados.";
        }
        catch (OperationCanceledException)
        {
            FinalStatus = "Generación cancelada. No se ha publicado un lote parcial.";
        }
        catch (PreparedInputsChangedException error)
        {
            FinalStatus = $"La revisión ha quedado obsoleta: {error.Message}";
            InvalidatePreparedReview();
            GeneralError = "Las entradas han cambiado. Analiza y revisa de nuevo antes de generar.";
            CurrentStep = 1;
        }
        catch (DocMergeException error)
        {
            FinalStatus = $"No se pudo generar el lote: {error.Message}";
        }
        catch (IOException error)
        {
            FinalStatus = $"No se pudo publicar el lote: {error.Message}";
        }
        catch (UnauthorizedAccessException error)
        {
            FinalStatus = $"No hay permisos para publicar el lote: {error.Message}";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _operationSequence++;
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    private bool CanAnalyze() => !IsBusy
        && HistoricalInput.HasSelection
        && RosterInput.HasSelection
        && TemplateInput.HasSelection;

    private bool CanGenerate() => CanContinueToGeneration
        && OutputDirectory is not null
        && OutputError is null
        && !HasHeaderErrors;

    private ClassGenerationOutputOptions? CreateOutputOptions()
    {
        if (_prepared is null || !IsHeaderCustomizationAvailable)
        {
            return null;
        }

        var headers = ClassHeaders.ToDictionary(
            item => item.ClassIdentifier,
            item => item.CreateValues());
        return new ClassGenerationOutputOptions(_prepared, headers);
    }

    private void BrowseOutput()
    {
        var selected = _dialogs.SelectFolder("Selecciona dónde crear la carpeta nueva", OutputParent);
        if (selected is not null)
        {
            OutputParent = selected;
        }
    }

    private void OpenOutput()
    {
        if (PublishedDirectory is not null)
        {
            _folderService.Open(PublishedDirectory);
        }
    }

    private void BuildReview(PreparedClassGeneration prepared)
    {
        ClearClassHeaders();
        Classes.Clear();
        Issues.Clear();
        var issues = prepared.Plan.Issues.Select(CreateReviewIssue).ToArray();
        foreach (var issue in issues)
        {
            Issues.Add(issue);
        }

        foreach (var classPlan in prepared.Plan.Classes)
        {
            var classIssues = issues.Where(issue => AffectsClass(
                issue.Issue, classPlan.Roster.ClassIdentifier)).ToArray();
            var students = classPlan.Rows.Select(row => new StudentReviewViewModel(
                classPlan.Roster.ClassIdentifier,
                row,
                classIssues.Where(issue => AffectsStudent(issue.Issue, row)).ToArray())).ToArray();
            Classes.Add(new ClassReviewViewModel(classPlan, students, classIssues));
            var header = new ClassHeaderViewModel(
                classPlan.Roster.ClassIdentifier,
                prepared.Template.SupportsDocumentHeader);
            header.ErrorsChanged += OnHeaderErrorsChanged;
            ClassHeaders.Add(header);
        }

        SelectedClass = Classes.FirstOrDefault();
        OnPropertyChanged(nameof(BatchSummary));
        OnPropertyChanged(nameof(HasBlockingIssues));
        OnPropertyChanged(nameof(CanContinueToGeneration));
        OnPropertyChanged(nameof(IsHeaderCustomizationAvailable));
        OnPropertyChanged(nameof(HeaderCustomizationMessage));
        OnPropertyChanged(nameof(HasHeaderErrors));
        RefreshCommands();
    }

    private void ApplyStudentFilter()
    {
        VisibleStudents.Clear();
        if (SelectedClass is null)
        {
            OnPropertyChanged(nameof(VisibleStudentSummary));
            OnPropertyChanged(nameof(HasVisibleStudents));
            return;
        }

        var search = SearchText.Trim();
        foreach (var student in SelectedClass.Students.Where(student =>
            MatchesSearch(student, search) && MatchesStatus(student)))
        {
            VisibleStudents.Add(student);
        }

        OnPropertyChanged(nameof(VisibleStudentSummary));
        OnPropertyChanged(nameof(HasVisibleStudents));
    }

    private static bool MatchesSearch(StudentReviewViewModel student, string search) =>
        search.Length == 0
        || student.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || student.StudentNumber.Contains(search, StringComparison.OrdinalIgnoreCase);

    private bool MatchesStatus(StudentReviewViewModel student) => SelectedFilter switch
    {
        AllStatusesFilter => true,
        RecoveredFilter => student.ResolutionType is HistoricalResolutionType.UniqueMatch
            or HistoricalResolutionType.EquivalentMatches,
        UniqueMatchFilter => student.ResolutionType == HistoricalResolutionType.UniqueMatch,
        EquivalentMatchesFilter => student.ResolutionType == HistoricalResolutionType.EquivalentMatches,
        MissingFilter => student.ResolutionType == HistoricalResolutionType.MissingHistoricalStudent,
        UnresolvedFilter => student.ResolutionType is HistoricalResolutionType.ConflictingHistoricalData
            or HistoricalResolutionType.AmbiguousStudentIdentity,
        ConflictingDataFilter => student.ResolutionType == HistoricalResolutionType.ConflictingHistoricalData,
        AmbiguousIdentityFilter => student.ResolutionType == HistoricalResolutionType.AmbiguousStudentIdentity,
        _ => false,
    };

    private static ReviewIssueViewModel CreateReviewIssue(GenerationIssue issue)
    {
        var title = issue.Code switch
        {
            "MissingHistoricalStudent" => "Sin histórico",
            "EquivalentHistoricalRecords" => "Históricos equivalentes",
            "ConflictingHistoricalData" => "Conflicto histórico",
            "AmbiguousStudentIdentity" => "Identidad ambigua",
            "PdfWithoutDeclaredTotal" => "PDF sin total declarado",
            "DuplicateClass" => "Clase duplicada",
            "DuplicateStudentNumber" => "NIA repetido",
            _ => issue.Code,
        };
        var consequence = issue.Code switch
        {
            "MissingHistoricalStudent" or "ConflictingHistoricalData" or "AmbiguousStudentIdentity" =>
                "Se generará el nombre del PDF; las demás columnas quedarán vacías y señalizadas.",
            "DuplicateClass" or "DuplicateStudentNumber" =>
                "Impide generar hasta corregir los documentos de entrada y analizar de nuevo.",
            _ => "Permite generar; revise las fuentes indicadas.",
        };
        return new ReviewIssueViewModel(
            issue,
            title,
            issue.StudentName ?? issue.ClassIdentifier?.CanonicalCode ?? "Lote completo",
            consequence,
            issue.Severity == GenerationIssueSeverity.Blocking);
    }

    private static bool AffectsClass(GenerationIssue issue, ClassIdentifier classIdentifier) =>
        issue.ClassIdentifier == classIdentifier
        || issue.SourceReferences.Any(reference => reference.ClassIdentifier == classIdentifier);

    private static bool AffectsStudent(GenerationIssue issue, StudentRowPlan row) =>
        issue.SourceReferences.Any(reference => string.Equals(
            reference.StudentNumber,
            row.RosterEntry.StudentNumber,
            StringComparison.Ordinal));

    private void ShowIssue(object? parameter)
    {
        if (parameter is not ReviewIssueViewModel issue)
        {
            return;
        }

        var references = issue.Issue.SourceReferences.Count > 0
            ? issue.Issue.SourceReferences.Select(reference =>
                $"{reference.SourcePath}: {reference.Location}")
            : issue.Issue.References;
        var body = $"{issue.Issue.Detail}{Environment.NewLine}{Environment.NewLine}"
            + $"Consecuencia: {issue.Consequence}{Environment.NewLine}{Environment.NewLine}"
            + string.Join(Environment.NewLine, references.Select(reference => $"• {reference}"));
        DetailRequested?.Invoke(this, new DetailRequestedEventArgs(issue.Title, body));
    }

    private void ShowStudent(object? parameter)
    {
        if (parameter is StudentReviewViewModel student)
        {
            DetailRequested?.Invoke(this, new DetailRequestedEventArgs(student.Name, student.Detail));
        }
    }

    private void ShowIssueGroup(object? parameter)
    {
        if (parameter is not ClassIssueGroupViewModel { Issues.Count: > 0 } group)
        {
            return;
        }

        ShowIssues($"{group.Title} ({group.Issues.Count})", group.Issues);
    }

    private void ShowAllIssues()
    {
        ShowIssues($"Incidencias del lote ({Issues.Count})", Issues);
    }

    private void ShowIssues(string title, IEnumerable<ReviewIssueViewModel> issues)
    {
        var body = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            issues.Select(issue =>
                $"[{issue.Severity}] {issue.Title} — {issue.Scope}{Environment.NewLine}"
                + $"{issue.Issue.Detail}{Environment.NewLine}{issue.Consequence}"));
        DetailRequested?.Invoke(this, new DetailRequestedEventArgs(title, body));
    }

    private void FilterByStatus(object? parameter)
    {
        if (parameter is not string filter || !StatusFilters.Contains(filter, StringComparer.Ordinal))
        {
            return;
        }

        SelectedFilter = SelectedFilter == filter ? AllStatusesFilter : filter;
    }

    private void ClearReviewFilters()
    {
        SearchText = string.Empty;
        SelectedFilter = AllStatusesFilter;
    }

    private void InvalidatePreparedReview()
    {
        _inputRevision++;
        _prepared = null;
        Classes.Clear();
        Issues.Clear();
        ClearClassHeaders();
        VisibleStudents.Clear();
        SelectedClass = null;
        PublishedDirectory = null;
        FinalStatus = null;
        GeneralError = null;
        OnPropertyChanged(nameof(BatchSummary));
        OnPropertyChanged(nameof(HasBlockingIssues));
        OnPropertyChanged(nameof(CanContinueToGeneration));
        OnPropertyChanged(nameof(IsHeaderCustomizationAvailable));
        OnPropertyChanged(nameof(HeaderCustomizationMessage));
        OnPropertyChanged(nameof(HasHeaderErrors));
        CurrentStep = 1;
        RefreshCommands();
    }

    private void ClearClassHeaders()
    {
        foreach (var header in ClassHeaders)
        {
            header.ErrorsChanged -= OnHeaderErrorsChanged;
        }

        ClassHeaders.Clear();
    }

    private void OnHeaderErrorsChanged(object? sender, System.ComponentModel.DataErrorsChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasHeaderErrors));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    private void StartInputAnalysis()
    {
        HistoricalInput.StartAnalysis();
        RosterInput.StartAnalysis();
        TemplateInput.StartAnalysis();
    }

    private void CompleteInputAnalysis(PreparedClassGeneration prepared)
    {
        HistoricalInput.CompleteAnalysis(prepared.Inputs.HistoricalDocumentPaths.Count);
        RosterInput.CompleteAnalysis(prepared.Inputs.RosterPdfPaths.Count);
        TemplateInput.CompleteAnalysis(count: null);
    }

    private void ResetInputAfterCancellation()
    {
        foreach (var input in Inputs())
        {
            if (input.HasSelection)
            {
                input.Select(input.Path!);
            }
        }
    }

    private void StopInputAnalysis()
    {
        foreach (var input in Inputs())
        {
            input.StopAnalysis();
        }
    }

    private void SetSelectionError(InputSelectionViewModel input, string message)
    {
        var detail = input.HasSelection
            ? $"{message} Se conserva la selección anterior."
            : message;
        input.SetError(detail);
    }

    private void SetTheme(object? parameter)
    {
        if (parameter is ThemePreference preference)
        {
            SelectedThemePreference = preference;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _selectedThemePreference = _themeService.Preference;
        OnPropertyChanged(nameof(SelectedThemePreference));
        IsDark = _themeService.IsDark;
        OnPropertyChanged(nameof(ThemeSummary));
        NotifyThemeSelectionChanged();
    }

    private void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(IsSidebarOverlayMode));
        OnPropertyChanged(nameof(IsSidebarOverlayVisible));
        OnPropertyChanged(nameof(IsCompactReviewLayout));
    }

    private void NotifyThemeSelectionChanged()
    {
        OnPropertyChanged(nameof(IsSystemThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
    }

    private void NotifyReviewFilterChanged()
    {
        OnPropertyChanged(nameof(HasActiveReviewFilters));
        OnPropertyChanged(nameof(ActiveFilterSummary));
        ClearReviewFiltersCommand.NotifyCanExecuteChanged();
    }

    private void SetInputError(GenerationInputException error)
    {
        if (error.InputType == GenerationInputType.InputSet)
        {
            GeneralError = error.Message;
            ResetInputAfterCancellation();
            return;
        }

        GetInput(error.InputType switch
        {
            GenerationInputType.HistoricalDocuments => InputKind.HistoricalDocuments,
            GenerationInputType.Rosters => InputKind.Rosters,
            GenerationInputType.Template => InputKind.Template,
            _ => throw new InvalidOperationException("Tipo de entrada no reconocido."),
        }).SetError(error.Message);
    }

    private void ReportProgress(ClassGenerationProgress progress)
    {
        ProgressText = progress.Stage switch
        {
            ClassGenerationStage.DiscoveringInputs => "Buscando archivos compatibles…",
            ClassGenerationStage.ReadingTemplate => "Comprobando la plantilla…",
            ClassGenerationStage.ReadingRosters => UnitProgress("Leyendo listados PDF", progress),
            ClassGenerationStage.ReadingHistoricalDocuments => UnitProgress("Indexando históricos", progress),
            ClassGenerationStage.Planning => "Preparando clases e incidencias…",
            ClassGenerationStage.VerifyingInputs => "Comprobando que las entradas no han cambiado…",
            ClassGenerationStage.GeneratingDocuments => "Generando documentos en una carpeta temporal…",
            ClassGenerationStage.ValidatingDocuments => "Validando los documentos generados…",
            ClassGenerationStage.Publishing => "Publicando el lote validado…",
            ClassGenerationStage.Completed => "Operación completada.",
            _ => string.Empty,
        };
    }

    private IProgress<ClassGenerationProgress> CreateProgress(int operation) =>
        new Progress<ClassGenerationProgress>(progress =>
        {
            if (operation == _operationSequence)
            {
                ReportProgress(progress);
            }
        });

    private static string UnitProgress(string label, ClassGenerationProgress progress) =>
        progress.Total.HasValue ? $"{label}: {progress.Completed} de {progress.Total.Value}" : $"{label}…";

    private static string CountText(int count, string singular, string plural) =>
        $"{count} {(count == 1 ? singular : plural)}";

    private void HandleUnexpectedError(Exception error)
    {
        GeneralError = $"Se produjo un error inesperado: {error.Message}";
        IsBusy = false;
    }

    private void RefreshCommands()
    {
        AnalyzeCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        BrowseInputCommand.NotifyCanExecuteChanged();
        RemoveInputCommand.NotifyCanExecuteChanged();
        RestoreIncludedTemplateCommand.NotifyCanExecuteChanged();
        ContinueToGenerationCommand.NotifyCanExecuteChanged();
        ShowAllIssuesCommand.NotifyCanExecuteChanged();
        BackToFilesCommand.NotifyCanExecuteChanged();
        BackToReviewCommand.NotifyCanExecuteChanged();
        BrowseOutputCommand.NotifyCanExecuteChanged();
    }

    private InputSelectionViewModel GetInput(InputKind kind) => kind switch
    {
        InputKind.HistoricalDocuments => HistoricalInput,
        InputKind.Rosters => RosterInput,
        InputKind.Template => TemplateInput,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private IEnumerable<InputSelectionViewModel> Inputs()
    {
        yield return HistoricalInput;
        yield return RosterInput;
        yield return TemplateInput;
    }

    private static string? ValidateImmediate(InputKind kind, string path)
    {
        if (kind == InputKind.Template)
        {
            if (!File.Exists(path))
            {
                return "Selecciona un archivo que exista.";
            }

            return string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase)
                ? null
                : "La plantilla debe ser un archivo .docx.";
        }

        return Directory.Exists(path) ? null : "Selecciona una carpeta que exista.";
    }

    private static string? InitialDirectory(string? path) => File.Exists(path)
        ? Path.GetDirectoryName(path)
        : path;

    private static bool IsValidFolderName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length > 0
            && trimmed is not "." and not ".."
            && !Path.IsPathRooted(trimmed)
            && trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !trimmed.Contains(Path.DirectorySeparatorChar)
            && !trimmed.Contains(Path.AltDirectorySeparatorChar);
    }
}

/// <summary>Contains content for one temporary read-only detail dialog.</summary>
public sealed class DetailRequestedEventArgs(string title, string body) : EventArgs
{
    /// <summary>Gets the dialog title.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the dialog body.</summary>
    public string Body { get; } = body;
}
