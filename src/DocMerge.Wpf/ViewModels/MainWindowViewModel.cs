using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private const string BundledTemplateDisplayName = "Plantilla predeterminada";
    private const double CompactSidebarWidth = 64;
    private const double ExpandedSidebarWidth = 220;
    private const double SidebarOverlayThreshold = 900;
    private const double CompactReviewThreshold = 1240;
    private const string AllStatusesFilter = "Todos los estados";
    private const string FoundFilter = "Encontrado";
    private const string NewStudentFilter = "Alumno nuevo";
    private const string ConflictFilter = "Conflicto";
    private readonly ClassGenerationService _preparationService;
    private readonly ClassGenerationExecutionService _executionService;
    private readonly IFileDialogService _dialogs;
    private readonly IThemeService _themeService;
    private readonly IDesktopPreferencesService _preferences;
    private readonly ISystemFolderService _folderService;
    private readonly IBundledGenerationTemplateProvider? _bundledTemplateProvider;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly object _operationGate = new();
    private PreparedClassGeneration? _prepared;
    private CancellationTokenSource? _operationCancellation;
    private int _inputRevision;
    private int _operationSequence;
    private int _sessionSequence;
    private DesktopWorkflowState _workflowState = DesktopWorkflowState.Selecting;
    private bool _canCancelActiveOperation;
    private bool _isCancellationRequested;
    private TaskCompletionSource? _operationCompletion;
    private TaskCompletionSource? _sessionUpdateCompletion;
    private bool _isDark;
    private ThemePreference _selectedThemePreference;
    private bool _isSidebarExpanded;
    private double _windowWidth = 1280;
    private string? _generalError;
    private string _progressText = string.Empty;
    private ClassReviewViewModel? _selectedClass;
    private StudentReviewViewModel? _selectedStudent;
    private string _searchText = string.Empty;
    private string _selectedFilter = AllStatusesFilter;
    private string? _outputParent;
    private string _outputFolderName = "Clases generadas";
    private string? _finalStatus;
    private string? _outputOpenError;
    private GenerationResultViewModel? _generationResult;
    private bool _isBundledTemplateInitialized;

    /// <summary>Initializes the desktop workflow.</summary>
    public MainWindowViewModel(
        ClassGenerationService preparationService,
        ClassGenerationExecutionService executionService,
        IFileDialogService dialogs,
        IThemeService themeService,
        IDesktopPreferencesService preferences,
        ISystemFolderService folderService,
        IBundledGenerationTemplateProvider? bundledTemplateProvider = null,
        ISourceDocumentService? sourceDocumentService = null)
    {
        _preparationService = preparationService ?? throw new ArgumentNullException(nameof(preparationService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _bundledTemplateProvider = bundledTemplateProvider;
        StudentDetail = new StudentDetailViewModel(
            sourceDocumentService ?? new SourceDocumentService());
        StudentDetail.PropertyChanged += OnStudentDetailPropertyChanged;
        StudentDetail.StudentChanged += OnDetailStudentChanged;
        _synchronizationContext = SynchronizationContext.Current;
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
            BundledTemplateDisplayName);
        if (_bundledTemplateProvider is not null)
        {
            TemplateInput.SetBundledPending(BundledTemplateDisplayName);
        }

        BrowseInputCommand = new RelayCommand(BrowseInput, _ => CanModifyInputs);
        RemoveInputCommand = new RelayCommand(RemoveInput, _ => CanModifyInputs);
        RestoreIncludedTemplateCommand = new AsyncRelayCommand(
            RestoreIncludedTemplateAsync,
            () => CanModifyInputs && TemplateInput.CanRestoreBundled,
            HandleUnexpectedError);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, CanAnalyze, HandleUnexpectedError);
        CancelCommand = new RelayCommand(_ => CancelOperation(), _ => CanCancelOperation);
        BackToFilesCommand = new RelayCommand(_ => NavigateToFiles(), _ => WorkflowState == DesktopWorkflowState.Reviewing);
        ContinueToGenerationCommand = new RelayCommand(
            _ => NavigateToConfiguration(),
            _ => CanContinueToGeneration);
        BackToReviewCommand = new RelayCommand(_ => NavigateToReview(), _ => CanReturnToReview);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput(), _ => CanEditConfiguration);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, CanGenerate, HandleUnexpectedError);
        OpenOutputCommand = new RelayCommand(_ => OpenOutput(), _ => WorkflowState == DesktopWorkflowState.Completed);
        NewGenerationCommand = new AsyncRelayCommand(
            StartNewGenerationAsync,
            () => WorkflowState == DesktopWorkflowState.Completed,
            HandleUnexpectedError);
        ShowIssueCommand = new RelayCommand(ShowIssue);
        ShowAllIssuesCommand = new RelayCommand(_ => ShowAllIssues(), _ => Issues.Count > 0);
        ShowIssueGroupCommand = new RelayCommand(ShowIssueGroup);
        OpenStudentDetailCommand = new RelayCommand(OpenStudentDetail);
        FilterByStatusCommand = new RelayCommand(FilterByStatus);
        FilterClassByStatusCommand = new RelayCommand(FilterClassByStatus);
        ClearReviewFiltersCommand = new RelayCommand(_ => ClearReviewFilters(), _ => HasActiveReviewFilters);
        SetThemeCommand = new RelayCommand(SetTheme);
        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
        CloseSidebarOverlayCommand = new RelayCommand(_ => IsSidebarExpanded = false);
    }

    /// <summary>Raised when a temporary read-only issue dialog should be displayed.</summary>
    public event EventHandler<IssueDetailRequestedEventArgs>? IssueDetailRequested;

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

    /// <summary>Gets the integrated student-detail state.</summary>
    public StudentDetailViewModel StudentDetail { get; }

    /// <summary>Gets available review filters.</summary>
    public IReadOnlyList<string> StatusFilters { get; } =
    [
        AllStatusesFilter,
        FoundFilter,
        NewStudentFilter,
        ConflictFilter,
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

    /// <summary>Gets the command that starts a clean desktop session.</summary>
    public AsyncRelayCommand NewGenerationCommand { get; }

    /// <summary>Gets the command that shows issue detail.</summary>
    public RelayCommand ShowIssueCommand { get; }

    /// <summary>Gets the command that shows all batch issues.</summary>
    public RelayCommand ShowAllIssuesCommand { get; }

    /// <summary>Gets the command that shows one traceable class-issue subset.</summary>
    public RelayCommand ShowIssueGroupCommand { get; }

    /// <summary>Gets the command that opens integrated student detail.</summary>
    public RelayCommand OpenStudentDetailCommand { get; }

    /// <summary>Gets the command that activates a review-only status filter.</summary>
    public RelayCommand FilterByStatusCommand { get; }

    /// <summary>Gets the command that selects a class card and activates its status filter.</summary>
    public RelayCommand FilterClassByStatusCommand { get; }

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

    /// <summary>Gets the exclusive state of the current desktop session.</summary>
    public DesktopWorkflowState WorkflowState
    {
        get => _workflowState;
        private set
        {
            if (!SetProperty(ref _workflowState, value))
            {
                return;
            }

            NotifyWorkflowStateChanged();
        }
    }

    /// <summary>Gets the active one-based workflow step.</summary>
    public int CurrentStep => WorkflowState switch
    {
        DesktopWorkflowState.Reviewing => 2,
        DesktopWorkflowState.Configuring
            or DesktopWorkflowState.Generating
            or DesktopWorkflowState.CancellingGeneration
            or DesktopWorkflowState.Publishing
            or DesktopWorkflowState.Completed
            or DesktopWorkflowState.GenerationCancelled
            or DesktopWorkflowState.GenerationFailed => 3,
        _ => 1,
    };

    /// <summary>Gets whether selection is visible.</summary>
    public bool IsSelectionStep => CurrentStep == 1;

    /// <summary>Gets whether review is visible.</summary>
    public bool IsReviewStep => CurrentStep == 2;

    /// <summary>Gets whether final generation is visible.</summary>
    public bool IsGenerationStep => CurrentStep == 3;

    /// <summary>Gets the first step number or completion mark.</summary>
    public string StepOneIndicator => StepOneState == WorkflowStepState.Completed ? "✓" : "1";

    /// <summary>Gets the second step number or completion mark.</summary>
    public string StepTwoIndicator => StepTwoState == WorkflowStepState.Completed ? "✓" : "2";

    /// <summary>Gets the third step number or completion mark.</summary>
    public string StepThreeIndicator => StepThreeState == WorkflowStepState.Completed ? "✓" : "3";

    /// <summary>Gets the visual state of input selection.</summary>
    public WorkflowStepState StepOneState => CurrentStep == 1
        ? WorkflowStepState.Current
        : _prepared is null
            ? WorkflowStepState.Pending
            : WorkflowStepState.Completed;

    /// <summary>Gets the visual state of review.</summary>
    public WorkflowStepState StepTwoState => CurrentStep switch
    {
        < 2 => WorkflowStepState.Pending,
        2 => WorkflowStepState.Current,
        _ => WorkflowStepState.Completed,
    };

    /// <summary>Gets the visual state of document generation.</summary>
    public WorkflowStepState StepThreeState => WorkflowState == DesktopWorkflowState.Completed
        ? WorkflowStepState.Completed
        : CurrentStep == 3
            ? WorkflowStepState.Current
            : WorkflowStepState.Pending;

    /// <summary>Gets whether a long operation is active.</summary>
    public bool IsBusy => WorkflowState is
        DesktopWorkflowState.Initializing
        or DesktopWorkflowState.Analyzing
        or DesktopWorkflowState.CancellingAnalysis
        or DesktopWorkflowState.Generating
        or DesktopWorkflowState.CancellingGeneration
        or DesktopWorkflowState.Publishing;

    /// <summary>Gets whether analysis is active and can still be cancelled.</summary>
    public bool CanCancelAnalysis => WorkflowState == DesktopWorkflowState.Analyzing && CanCancelOperation;

    /// <summary>Gets whether generation is active and can still be cancelled.</summary>
    public bool CanCancelGeneration => WorkflowState == DesktopWorkflowState.Generating && CanCancelOperation;

    /// <summary>Gets whether a cancellation request is waiting for cleanup.</summary>
    public bool IsCancelling => WorkflowState is
        DesktopWorkflowState.CancellingAnalysis or DesktopWorkflowState.CancellingGeneration;

    /// <summary>Gets whether the generation form should be shown.</summary>
    public bool IsConfigurationVisible => WorkflowState is
        DesktopWorkflowState.Configuring
        or DesktopWorkflowState.GenerationCancelled
        or DesktopWorkflowState.GenerationFailed;

    /// <summary>Gets whether generation progress should replace the editable form.</summary>
    public bool IsGenerationOperationVisible => WorkflowState is
        DesktopWorkflowState.Generating
        or DesktopWorkflowState.CancellingGeneration
        or DesktopWorkflowState.Publishing;

    /// <summary>Gets whether the confirmed result view should be shown.</summary>
    public bool IsGenerationCompleted => WorkflowState == DesktopWorkflowState.Completed;

    /// <summary>Gets the current operation's non-pulsable primary label.</summary>
    public string OperationActionText => IsCancelling ? "Cancelando…" : "Generando…";

    /// <summary>Gets the selection step's context-sensitive primary label.</summary>
    public string AnalysisActionText => WorkflowState switch
    {
        DesktopWorkflowState.Analyzing => "Analizando…",
        DesktopWorkflowState.CancellingAnalysis => "Cancelando…",
        _ => "Analizar y continuar",
    };

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
    public bool IsCompactReviewLayout => AvailableReviewWidth < CompactReviewThreshold;

    /// <summary>Gets a value that indicates whether integrated student detail is open.</summary>
    public bool IsStudentDetailOpen => StudentDetail.IsOpen;

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
            if (!SetProperty(ref _selectedClass, value))
            {
                return;
            }

            StudentDetail.Close();
            SelectedStudent = null;
            ApplyStudentFilter();
            OnPropertyChanged(nameof(VisibleStudentSummary));
        }
    }

    /// <summary>Gets or sets the student selected in the review table.</summary>
    public StudentReviewViewModel? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (!SetProperty(ref _selectedStudent, value)
                || value is null
                || !StudentDetail.IsOpen)
            {
                return;
            }

            StudentDetail.ShowStudent(value);
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
        : HasActiveReviewFilters
            ? $"{VisibleStudents.Count} de {SelectedClass.StudentCount} alumnos coinciden · {SelectedClass.OutputSummary}"
            : SelectedClass.OutputSummary;

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
        : $"{CountText(_prepared.Plan.RosterStudentCount, "alumno en el listado", "alumnos en el listado")} · "
            + $"{_prepared.Plan.IncludedStudentCount} "
            + $"{(_prepared.Plan.IncludedStudentCount == 1 ? "se incluirá" : "se incluirán")} · "
            + $"{_prepared.Plan.OmittedStudentCount} "
            + $"{(_prepared.Plan.OmittedStudentCount == 1 ? "omitido" : "omitidos")} por conflicto · "
            + CountText(_prepared.Plan.OutputClasses.Count, "documento previsto", "documentos previstos");

    /// <summary>Gets whether blocking issues prevent final generation.</summary>
    public bool HasBlockingIssues => _prepared?.Plan.HasBlockingIssues == true;

    /// <summary>Gets guidance about whether the reviewed batch can continue.</summary>
    public string ReviewReadinessMessage => _prepared switch
    {
        null => string.Empty,
        { Plan.HasBlockingIssues: true } => "Corrige los bloqueos y vuelve a analizar antes de generar.",
        { Plan.OutputClasses.Count: 0 } => "No hay alumnos sin conflicto para generar documentos.",
        _ => "En el siguiente paso elegirás la carpeta de salida.",
    };

    /// <summary>Gets whether the selected template supports title and tutor fields.</summary>
    public bool IsHeaderCustomizationAvailable => _prepared?.Template.SupportsDocumentHeader == true;

    /// <summary>Gets guidance for the selected template's header support.</summary>
    public string HeaderCustomizationMessage => IsHeaderCustomizationAvailable
        ? "Si lo dejas vacío, se conserva su espacio en el documento."
        : "Esta plantilla es compatible con la generación, pero no admite personalizar la cabecera.";

    /// <summary>Gets whether any editable document header is invalid.</summary>
    public bool HasHeaderErrors => ClassHeaders.Any(item => item.HasErrors);

    /// <summary>Gets selection guidance consistent with the active template source.</summary>
    public string SelectionIntroduction => TemplateInput.IsBundled
        ? "Selecciona las carpetas de históricos y listados PDF. La plantilla predeterminada ya está incluida."
        : "Selecciona las carpetas de históricos y listados PDF. Se utilizará la plantilla externa elegida.";

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
                OutputOpenError = null;
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
                OutputOpenError = null;
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
                return "Selecciona dónde guardar la carpeta nueva.";
            }

            if (!Directory.Exists(OutputParent))
            {
                return "La ubicación seleccionada ya no está disponible.";
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

    /// <summary>Gets the directory from the last confirmed execution.</summary>
    public string? PublishedDirectory => GenerationResult?.OutputDirectory;

    /// <summary>Gets the immutable result of the confirmed execution.</summary>
    public GenerationResultViewModel? GenerationResult
    {
        get => _generationResult;
        private set
        {
            if (SetProperty(ref _generationResult, value))
            {
                OnPropertyChanged(nameof(PublishedDirectory));
                OnPropertyChanged(nameof(HasResultIssues));
                OpenOutputCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets whether the confirmed result contains structured issues.</summary>
    public bool HasResultIssues => GenerationResult?.HasIssues == true;

    /// <summary>Gets an error raised while opening an otherwise successful output.</summary>
    public string? OutputOpenError
    {
        get => _outputOpenError;
        private set => SetProperty(ref _outputOpenError, value);
    }

    /// <summary>Gets the final success, cancellation, or failure text.</summary>
    public string? FinalStatus
    {
        get => _finalStatus;
        private set => SetProperty(ref _finalStatus, value);
    }

    /// <summary>Gets the assembly product version.</summary>
    public string Version => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        ?? "1.0.0";

    /// <summary>Gets whether review can advance to final generation.</summary>
    public bool CanContinueToGeneration => WorkflowState == DesktopWorkflowState.Reviewing
        && _prepared is not null
        && !HasBlockingIssues
        && _prepared.Plan.OutputClasses.Count > 0;

    /// <summary>Gets an identity that changes whenever a new desktop session starts.</summary>
    public int SessionSequence => _sessionSequence;

    /// <summary>Gets whether the active operation can honestly accept cancellation.</summary>
    public bool CanCancelOperation
    {
        get
        {
            lock (_operationGate)
            {
                return _canCancelActiveOperation && !_isCancellationRequested;
            }
        }
    }

    /// <summary>Gets a task that completes after the active operation has returned and cleaned up.</summary>
    public Task WaitForActiveOperationAsync()
    {
        lock (_operationGate)
        {
            return _operationCompletion?.Task
                ?? _sessionUpdateCompletion?.Task
                ?? Task.CompletedTask;
        }
    }

    /// <summary>Prepares the included template without exposing its internal path.</summary>
    public async Task InitializeAsync()
    {
        if (_bundledTemplateProvider is null || _isBundledTemplateInitialized)
        {
            return;
        }

        _isBundledTemplateInitialized = true;
        var session = _sessionSequence;
        var completion = BeginSessionUpdate();
        WorkflowState = DesktopWorkflowState.Initializing;
        try
        {
            var template = await _bundledTemplateProvider.GetAsync(CancellationToken.None);
            if (session != _sessionSequence)
            {
                return;
            }

            TemplateInput.SelectBundled(template.Path, BundledTemplateDisplayName);
            WorkflowState = DesktopWorkflowState.Selecting;
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException
            or DocMergeException)
        {
            TemplateInput.SetError($"No se pudo preparar la plantilla incluida: {error.Message}");
            WorkflowState = DesktopWorkflowState.AnalysisFailed;
        }
        finally
        {
            EndSessionUpdate(completion);
        }

        RefreshCommands();
    }

    /// <summary>Accepts a path from drag and drop after validating its immediate type.</summary>
    public void SelectDroppedPath(InputKind kind, string path)
    {
        if (CanModifyInputs)
        {
            SelectInput(kind, path);
        }
    }

    /// <summary>Shows an inline drag-and-drop error without discarding an existing selection.</summary>
    public void RejectDroppedPath(InputKind kind, string message)
    {
        if (CanModifyInputs)
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
        CancellationTokenSource? cancellation;
        lock (_operationGate)
        {
            if (!_canCancelActiveOperation || _isCancellationRequested)
            {
                return;
            }

            _isCancellationRequested = true;
            cancellation = _operationCancellation;
        }

        WorkflowState = WorkflowState == DesktopWorkflowState.Analyzing
            ? DesktopWorkflowState.CancellingAnalysis
            : DesktopWorkflowState.CancellingGeneration;
        ProgressText = "Cancelando de forma segura…";
        cancellation?.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _themeService.Changed -= OnThemeChanged;
        StudentDetail.PropertyChanged -= OnStudentDetailPropertyChanged;
        StudentDetail.StudentChanged -= OnDetailStudentChanged;
        StudentDetail.Close();
        ClearClassHeaders();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void BrowseInput(object? parameter)
    {
        if (!CanModifyInputs || parameter is not InputSelectionViewModel input)
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
        OnPropertyChanged(nameof(SelectionIntroduction));
    }

    private async Task RestoreIncludedTemplateAsync()
    {
        if (_bundledTemplateProvider is null)
        {
            return;
        }

        var session = _sessionSequence;
        var previousState = WorkflowState;
        var completion = BeginSessionUpdate();
        WorkflowState = DesktopWorkflowState.Initializing;
        try
        {
            var template = await _bundledTemplateProvider.GetAsync(CancellationToken.None);
            if (session != _sessionSequence)
            {
                return;
            }

            if (TemplateInput.IsBundled
                && string.Equals(TemplateInput.Path, template.Path, StringComparison.OrdinalIgnoreCase))
            {
                WorkflowState = previousState;
                return;
            }

            TemplateInput.SelectBundled(template.Path, BundledTemplateDisplayName);
            InvalidatePreparedReview();
            OnPropertyChanged(nameof(SelectionIntroduction));
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException
            or DocMergeException)
        {
            GeneralError = $"No se pudo restaurar la plantilla incluida: {error.Message}";
            WorkflowState = previousState;
        }
        finally
        {
            EndSessionUpdate(completion);
        }
    }

    private void RemoveInput(object? parameter)
    {
        if (CanModifyInputs && parameter is InputSelectionViewModel { Kind: not InputKind.Template } input)
        {
            input.Clear();
            InvalidatePreparedReview();
        }
    }

    private async Task AnalyzeAsync()
    {
        InvalidatePreparedReview();
        var revision = _inputRevision;
        StartInputAnalysis();
        var (operation, cancellation) = BeginOperation();
        WorkflowState = DesktopWorkflowState.Analyzing;
        var progress = CreateOperationProgress(operation);
        var request = new ClassGenerationRequest(
            HistoricalInput.Path!, RosterInput.Path!, TemplateInput.Path!, StudentHeader);
        var completionState = DesktopWorkflowState.AnalysisFailed;
        try
        {
            var prepared = await Task.Run(
                () => _preparationService.PrepareAsync(
                    request, cancellation.Token, progress),
                cancellation.Token);
            if (revision != _inputRevision || WasCancellationRequested(operation))
            {
                throw new OperationCanceledException(cancellation.Token);
            }

            if (TemplateInput.IsBundled && !prepared.Template.SupportsDocumentHeader)
            {
                TemplateInput.SetError(
                    "La plantilla predeterminada no contiene los campos de clase y tutor requeridos.");
                GeneralError = "La plantilla incluida es incompatible. No se ha creado un plan de revisión.";
                completionState = DesktopWorkflowState.AnalysisFailed;
                return;
            }

            _prepared = prepared;
            CompleteInputAnalysis(prepared);
            BuildReview(prepared);
            completionState = DesktopWorkflowState.Reviewing;
        }
        catch (OperationCanceledException)
        {
            GeneralError = "Análisis cancelado. No se ha creado ningún plan de revisión.";
            completionState = DesktopWorkflowState.AnalysisCancelled;
        }
        catch (GenerationInputException error)
        {
            SetInputError(error);
            completionState = DesktopWorkflowState.AnalysisFailed;
        }
        catch (DocMergeException error)
        {
            GeneralError = error.Message;
            completionState = DesktopWorkflowState.AnalysisFailed;
        }
        finally
        {
            StopInputAnalysis();
            EndOperation(operation, cancellation);
            ProgressText = string.Empty;
            WorkflowState = completionState;
        }
    }

    private async Task GenerateAsync()
    {
        if (_prepared is null || OutputDirectory is null || !CanGenerate())
        {
            return;
        }

        var prepared = _prepared;
        var outputDirectory = OutputDirectory;
        var outputOptions = CreateOutputOptions();
        var resultHeaders = outputOptions?.Headers
            ?? new Dictionary<ClassIdentifier, ClassHeaderValues>();
        var templateName = TemplateInput.DisplayName;
        GeneralError = null;
        FinalStatus = null;
        GenerationResult = null;
        OutputOpenError = null;
        var (operation, cancellation) = BeginOperation();
        WorkflowState = DesktopWorkflowState.Generating;
        var progress = CreateOperationProgress(operation);
        var completionState = DesktopWorkflowState.GenerationFailed;
        GenerationResultViewModel? confirmedResult = null;
        var inputsChanged = false;
        try
        {
            var request = new ClassGenerationExecutionRequest(
                prepared,
                outputDirectory,
                SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier),
                outputOptions);
            var result = await Task.Run(
                () => _executionService.ExecuteAsync(
                    request, cancellation.Token, progress),
                cancellation.Token);
            confirmedResult = new GenerationResultViewModel(
                result, resultHeaders, templateName);
            completionState = DesktopWorkflowState.Completed;
        }
        catch (OperationCanceledException)
        {
            FinalStatus = "Generación cancelada. No se han guardado documentos parciales.";
            completionState = DesktopWorkflowState.GenerationCancelled;
        }
        catch (PreparedInputsChangedException error)
        {
            FinalStatus = $"La revisión ha quedado obsoleta: {error.Message}";
            GeneralError = "Las entradas han cambiado. Analiza y revisa de nuevo antes de generar.";
            inputsChanged = true;
            completionState = DesktopWorkflowState.AnalysisFailed;
        }
        catch (DocMergeException error)
        {
            FinalStatus = $"No se pudo generar el lote: {error.Message}";
            completionState = DesktopWorkflowState.GenerationFailed;
        }
        catch (IOException error)
        {
            FinalStatus = $"No se pudieron guardar los documentos: {error.Message}";
            completionState = DesktopWorkflowState.GenerationFailed;
        }
        catch (UnauthorizedAccessException error)
        {
            FinalStatus = $"No hay permisos para guardar los documentos: {error.Message}";
            completionState = DesktopWorkflowState.GenerationFailed;
        }
        finally
        {
            EndOperation(operation, cancellation);
            ProgressText = string.Empty;
            if (inputsChanged)
            {
                var finalStatus = FinalStatus;
                var generalError = GeneralError;
                InvalidatePreparedReview();
                FinalStatus = finalStatus;
                GeneralError = generalError;
                WorkflowState = completionState;
            }
            else
            {
                GenerationResult = confirmedResult;
                WorkflowState = completionState;
            }
        }
    }

    private bool CanAnalyze() => WorkflowState is
        (DesktopWorkflowState.Selecting
            or DesktopWorkflowState.AnalysisCancelled
            or DesktopWorkflowState.AnalysisFailed)
        && HistoricalInput.HasSelection
        && RosterInput.HasSelection
        && TemplateInput.HasSelection;

    private bool CanGenerate() => CanEditConfiguration
        && HistoricalInput.HasSelection
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
        if (!CanEditConfiguration)
        {
            return;
        }

        var selected = _dialogs.SelectFolder("Selecciona dónde crear la carpeta nueva", OutputParent);
        if (selected is not null)
        {
            OutputParent = selected;
        }
    }

    private void OpenOutput()
    {
        var directory = GenerationResult?.OutputDirectory;
        if (directory is null)
        {
            return;
        }

        OutputOpenError = null;
        if (!Directory.Exists(directory))
        {
            OutputOpenError = "La carpeta guardada ya no está disponible. Puede haberse movido o eliminado.";
            return;
        }

        try
        {
            _folderService.Open(directory);
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            OutputOpenError = $"No se pudo abrir la carpeta: {error.Message}";
        }
    }

    private void NavigateToFiles()
    {
        if (WorkflowState == DesktopWorkflowState.Reviewing)
        {
            InvalidatePreparedReview();
        }
    }

    private void NavigateToConfiguration()
    {
        if (CanContinueToGeneration)
        {
            StudentDetail.Close();
            WorkflowState = DesktopWorkflowState.Configuring;
        }
    }

    private void NavigateToReview()
    {
        if (CanReturnToReview)
        {
            WorkflowState = DesktopWorkflowState.Reviewing;
        }
    }

    private async Task StartNewGenerationAsync()
    {
        if (WorkflowState != DesktopWorkflowState.Completed)
        {
            return;
        }

        var session = ++_sessionSequence;
        var completion = BeginSessionUpdate();
        try
        {
            _inputRevision++;
            _prepared = null;
            ClearClassHeaders();
            Classes.Clear();
            Issues.Clear();
            VisibleStudents.Clear();
            StudentDetail.Close();
            SelectedStudent = null;
            SelectedClass = null;
            ClearReviewFilters();
            HistoricalInput.Clear();
            RosterInput.Clear();
            TemplateInput.Clear();
            GenerationResult = null;
            FinalStatus = null;
            GeneralError = null;
            OutputOpenError = null;
            OutputParent = null;
            OutputFolderName = "Clases generadas";
            ProgressText = string.Empty;
            WorkflowState = DesktopWorkflowState.Initializing;

            if (_bundledTemplateProvider is null)
            {
                WorkflowState = DesktopWorkflowState.Selecting;
                return;
            }

            TemplateInput.SetBundledPending(BundledTemplateDisplayName);
            try
            {
                var template = await _bundledTemplateProvider.GetAsync(CancellationToken.None);
                if (session != _sessionSequence)
                {
                    return;
                }

                TemplateInput.SelectBundled(template.Path, BundledTemplateDisplayName);
                WorkflowState = DesktopWorkflowState.Selecting;
            }
            catch (Exception error) when (error is IOException
                or UnauthorizedAccessException
                or DocMergeException)
            {
                if (session != _sessionSequence)
                {
                    return;
                }

                TemplateInput.SetError($"No se pudo restaurar la plantilla incluida: {error.Message}");
                WorkflowState = DesktopWorkflowState.AnalysisFailed;
            }
        }
        finally
        {
            EndSessionUpdate(completion);
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
                classIssues.Where(issue => AffectsStudent(issue.Issue, row)).ToArray(),
                classPlan.Roster.SourcePath)).ToArray();
            Classes.Add(new ClassReviewViewModel(classPlan, students, classIssues));
            if (!classPlan.ProducesDocument)
            {
                continue;
            }

            var header = new ClassHeaderViewModel(
                classPlan.Roster.ClassIdentifier,
                prepared.Template.SupportsDocumentHeader);
            header.ErrorsChanged += OnHeaderErrorsChanged;
            ClassHeaders.Add(header);
        }

        SelectedClass = Classes.FirstOrDefault();
        OnPropertyChanged(nameof(BatchSummary));
        OnPropertyChanged(nameof(HasBlockingIssues));
        OnPropertyChanged(nameof(ReviewReadinessMessage));
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
            StudentDetail.UpdateVisibleStudents(VisibleStudents, GetStudentNavigationContext());
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
        StudentDetail.UpdateVisibleStudents(VisibleStudents, GetStudentNavigationContext());
    }

    private static bool MatchesSearch(StudentReviewViewModel student, string search) =>
        search.Length == 0
        || student.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || student.StudentNumber.Contains(search, StringComparison.OrdinalIgnoreCase);

    private bool MatchesStatus(StudentReviewViewModel student) => SelectedFilter switch
    {
        AllStatusesFilter => true,
        FoundFilter => student.ResolutionType == HistoricalResolutionType.Found,
        NewStudentFilter => student.ResolutionType == HistoricalResolutionType.NewStudent,
        ConflictFilter => student.ResolutionType == HistoricalResolutionType.Conflict,
        _ => false,
    };

    private static ReviewIssueViewModel CreateReviewIssue(GenerationIssue issue)
    {
        var title = issue.Code switch
        {
            "DuplicateStudentName" => "Nombre y apellidos duplicados",
            "DuplicateClass" => "Clase duplicada",
            "DuplicateStudentNumber" => "NIA repetido",
            _ => issue.Code,
        };
        var consequence = issue.Code switch
        {
            "DuplicateStudentName" =>
                "El alumno se omitirá de los documentos generados hasta resolver el conflicto.",
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
        IssueDetailRequested?.Invoke(this, new IssueDetailRequestedEventArgs(issue.Title, body));
    }

    private void OpenStudentDetail(object? parameter)
    {
        if (parameter is not StudentReviewViewModel student
            || !VisibleStudents.Contains(student))
        {
            return;
        }

        SelectedStudent = student;
        StudentDetail.Open(student, VisibleStudents, GetStudentNavigationContext());
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
        IssueDetailRequested?.Invoke(this, new IssueDetailRequestedEventArgs(title, body));
    }

    private void FilterByStatus(object? parameter)
    {
        if (parameter is not string filter || !StatusFilters.Contains(filter, StringComparer.Ordinal))
        {
            return;
        }

        SelectedFilter = SelectedFilter == filter ? AllStatusesFilter : filter;
    }

    private void FilterClassByStatus(object? parameter)
    {
        if (parameter is not ClassStatusFilterRequest request
            || !Classes.Contains(request.ClassReview))
        {
            return;
        }

        var filter = StudentReviewText.Status(request.ResolutionType);
        if (!StatusFilters.Contains(filter, StringComparer.Ordinal))
        {
            return;
        }

        var togglesCurrentFilter = ReferenceEquals(SelectedClass, request.ClassReview)
            && SelectedFilter == filter;
        SelectedClass = request.ClassReview;
        SelectedFilter = togglesCurrentFilter ? AllStatusesFilter : filter;
    }

    private StudentNavigationContext GetStudentNavigationContext() => HasActiveReviewFilters
        ? StudentNavigationContext.FilteredResults
        : StudentNavigationContext.CompleteClass;

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
        StudentDetail.Close();
        SelectedStudent = null;
        SelectedClass = null;
        ClearReviewFilters();
        foreach (var input in Inputs())
        {
            input.ResetAnalysis();
        }

        GenerationResult = null;
        FinalStatus = null;
        OutputOpenError = null;
        GeneralError = null;
        ProgressText = string.Empty;
        OutputParent = null;
        OutputFolderName = "Clases generadas";
        OnPropertyChanged(nameof(BatchSummary));
        OnPropertyChanged(nameof(HasBlockingIssues));
        OnPropertyChanged(nameof(ReviewReadinessMessage));
        OnPropertyChanged(nameof(CanContinueToGeneration));
        OnPropertyChanged(nameof(IsHeaderCustomizationAvailable));
        OnPropertyChanged(nameof(HeaderCustomizationMessage));
        OnPropertyChanged(nameof(HasHeaderErrors));
        WorkflowState = DesktopWorkflowState.Selecting;
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

    private void OnStudentDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StudentDetailViewModel.IsOpen))
        {
            return;
        }

        OnPropertyChanged(nameof(IsStudentDetailOpen));
    }

    private void OnDetailStudentChanged(object? sender, StudentDetailStudentChangedEventArgs e) =>
        SelectedStudent = e.Student;

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

    private bool CanModifyInputs => WorkflowState is
        DesktopWorkflowState.Selecting
        or DesktopWorkflowState.AnalysisCancelled
        or DesktopWorkflowState.AnalysisFailed
        or DesktopWorkflowState.Configuring
        or DesktopWorkflowState.GenerationCancelled
        or DesktopWorkflowState.GenerationFailed;

    private bool CanEditConfiguration => WorkflowState is
        DesktopWorkflowState.Configuring
        or DesktopWorkflowState.GenerationCancelled
        or DesktopWorkflowState.GenerationFailed;

    private bool CanReturnToReview => CanEditConfiguration && _prepared is not null;

    private double AvailableReviewWidth => Math.Max(0, _windowWidth - SidebarColumnWidth);

    private TaskCompletionSource BeginSessionUpdate()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_operationGate)
        {
            _sessionUpdateCompletion = completion;
        }

        return completion;
    }

    private void EndSessionUpdate(TaskCompletionSource completion)
    {
        lock (_operationGate)
        {
            if (ReferenceEquals(_sessionUpdateCompletion, completion))
            {
                _sessionUpdateCompletion = null;
            }
        }

        completion.TrySetResult();
    }

    private (int Operation, CancellationTokenSource Cancellation) BeginOperation()
    {
        var cancellation = new CancellationTokenSource();
        lock (_operationGate)
        {
            _operationCancellation = cancellation;
            _canCancelActiveOperation = true;
            _isCancellationRequested = false;
            _operationCompletion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _operationSequence++;
            return (_operationSequence, cancellation);
        }
    }

    private void EndOperation(int operation, CancellationTokenSource cancellation)
    {
        TaskCompletionSource? completion = null;
        lock (_operationGate)
        {
            if (operation == _operationSequence)
            {
                _canCancelActiveOperation = false;
                _isCancellationRequested = false;
                _operationCancellation = null;
                completion = _operationCompletion;
                _operationCompletion = null;
                _operationSequence++;
            }
        }

        cancellation.Dispose();
        completion?.TrySetResult();
    }

    private bool WasCancellationRequested(int operation)
    {
        lock (_operationGate)
        {
            return operation != _operationSequence || _isCancellationRequested;
        }
    }

    private void DisplayProgress(int operation, ClassGenerationProgress progress)
    {
        lock (_operationGate)
        {
            if (operation != _operationSequence || _isCancellationRequested)
            {
                return;
            }
        }

        if (progress.Stage == ClassGenerationStage.Publishing
            && WorkflowState == DesktopWorkflowState.Generating)
        {
            WorkflowState = DesktopWorkflowState.Publishing;
        }

        ReportProgress(progress);
    }

    private void Dispatch(Action action)
    {
        if (_synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), state: null);
    }

    private void NotifyWorkflowStateChanged()
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsSelectionStep));
        OnPropertyChanged(nameof(IsReviewStep));
        OnPropertyChanged(nameof(IsGenerationStep));
        OnPropertyChanged(nameof(StepOneIndicator));
        OnPropertyChanged(nameof(StepTwoIndicator));
        OnPropertyChanged(nameof(StepThreeIndicator));
        OnPropertyChanged(nameof(StepOneState));
        OnPropertyChanged(nameof(StepTwoState));
        OnPropertyChanged(nameof(StepThreeState));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanCancelAnalysis));
        OnPropertyChanged(nameof(CanCancelGeneration));
        OnPropertyChanged(nameof(IsCancelling));
        OnPropertyChanged(nameof(IsConfigurationVisible));
        OnPropertyChanged(nameof(IsGenerationOperationVisible));
        OnPropertyChanged(nameof(IsGenerationCompleted));
        OnPropertyChanged(nameof(OperationActionText));
        OnPropertyChanged(nameof(AnalysisActionText));
        OnPropertyChanged(nameof(CanContinueToGeneration));
        RefreshCommands();
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
            ClassGenerationStage.Publishing => "Guardando los documentos…",
            ClassGenerationStage.Completed => "Confirmando el resultado…",
            _ => string.Empty,
        };
    }

    private IProgress<ClassGenerationProgress> CreateOperationProgress(int operation) =>
        new SynchronousProgress<ClassGenerationProgress>(progress =>
        {
            var shouldDisplay = false;
            lock (_operationGate)
            {
                if (operation != _operationSequence || _isCancellationRequested)
                {
                    return;
                }

                if (progress.Stage == ClassGenerationStage.Publishing)
                {
                    _canCancelActiveOperation = false;
                }

                shouldDisplay = true;
            }

            if (!shouldDisplay)
            {
                return;
            }

            Dispatch(() => DisplayProgress(operation, progress));
        });

    private static string UnitProgress(string label, ClassGenerationProgress progress) =>
        progress.Total.HasValue ? $"{label}: {progress.Completed} de {progress.Total.Value}" : $"{label}…";

    private static string CountText(int count, string singular, string plural) =>
        $"{count} {(count == 1 ? singular : plural)}";

    private void HandleUnexpectedError(Exception error)
    {
        GeneralError = $"Se produjo un error inesperado: {error.Message}";
        if (IsBusy)
        {
            WorkflowState = CurrentStep == 3
                ? DesktopWorkflowState.GenerationFailed
                : DesktopWorkflowState.AnalysisFailed;
        }
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
        OpenOutputCommand.NotifyCanExecuteChanged();
        NewGenerationCommand.NotifyCanExecuteChanged();
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

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

/// <summary>Contains content for one temporary read-only detail dialog.</summary>
public sealed class IssueDetailRequestedEventArgs(string title, string body) : EventArgs
{
    /// <summary>Gets the dialog title.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the dialog body.</summary>
    public string Body { get; } = body;
}
