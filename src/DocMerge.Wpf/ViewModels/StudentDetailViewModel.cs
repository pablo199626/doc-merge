using System.ComponentModel;
using System.IO;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Coordinates the integrated read-only detail for one visible student.</summary>
public sealed class StudentDetailViewModel : ViewModelBase
{
    private const string NoMeasuresMessage = "No hay medidas registradas";
    private readonly ISourceDocumentService _sourceDocumentService;
    private IReadOnlyList<StudentReviewViewModel> _visibleStudents = [];
    private StudentReviewViewModel? _student;
    private HistoricalStudentCandidate? _inspectedCandidate;
    private bool _isOpen;
    private string? _sourceOpenError;

    /// <summary>Initializes a new instance of the <see cref="StudentDetailViewModel"/> class.</summary>
    /// <param name="sourceDocumentService">The service that opens reviewed source documents.</param>
    public StudentDetailViewModel(ISourceDocumentService sourceDocumentService)
    {
        _sourceDocumentService = sourceDocumentService
            ?? throw new ArgumentNullException(nameof(sourceDocumentService));
        CloseCommand = new RelayCommand(_ => Close(), _ => IsOpen);
        PreviousStudentCommand = new RelayCommand(_ => NavigateStudent(-1), _ => CanNavigatePrevious);
        NextStudentCommand = new RelayCommand(_ => NavigateStudent(1), _ => CanNavigateNext);
        PreviousCandidateCommand = new RelayCommand(_ => NavigateCandidate(-1), _ => CanNavigatePreviousCandidate);
        NextCandidateCommand = new RelayCommand(_ => NavigateCandidate(1), _ => CanNavigateNextCandidate);
        InspectCandidateCommand = new RelayCommand(InspectCandidate);
        OpenSourceCommand = new RelayCommand(OpenSource);
    }

    /// <summary>Occurs when detail navigation changes the active student.</summary>
    public event EventHandler<StudentDetailStudentChangedEventArgs>? StudentChanged;

    /// <summary>Gets the command that closes the integrated detail.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>Gets the command that shows the previous visible student.</summary>
    public RelayCommand PreviousStudentCommand { get; }

    /// <summary>Gets the command that shows the next visible student.</summary>
    public RelayCommand NextStudentCommand { get; }

    /// <summary>Gets the command that inspects the previous historical candidate.</summary>
    public RelayCommand PreviousCandidateCommand { get; }

    /// <summary>Gets the command that inspects the next historical candidate.</summary>
    public RelayCommand NextCandidateCommand { get; }

    /// <summary>Gets the command that inspects one historical candidate.</summary>
    public RelayCommand InspectCandidateCommand { get; }

    /// <summary>Gets the command that opens a reviewed source document.</summary>
    public RelayCommand OpenSourceCommand { get; }

    /// <summary>Gets a value that indicates whether the detail is open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetProperty(ref _isOpen, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>Gets the student currently displayed.</summary>
    public StudentReviewViewModel? Student
    {
        get => _student;
        private set
        {
            if (!SetProperty(ref _student, value))
            {
                return;
            }

            SourceOpenError = null;
            InspectedCandidate = InitialCandidate(value);
            NotifyStudentChanged();
        }
    }

    /// <summary>Gets the historical candidate currently displayed for inspection.</summary>
    public HistoricalStudentCandidate? InspectedCandidate
    {
        get => _inspectedCandidate;
        private set
        {
            if (!SetProperty(ref _inspectedCandidate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(MeasuresContent));
            OnPropertyChanged(nameof(HasMeasures));
            OnPropertyChanged(nameof(MeasuresEmptyMessage));
            OnPropertyChanged(nameof(CandidatePositionText));
            OnPropertyChanged(nameof(Provenance));
            RefreshCommands();
        }
    }

    /// <summary>Gets the visible PDF name.</summary>
    public string Name => Student?.Name ?? string.Empty;

    /// <summary>Gets the student number from the PDF.</summary>
    public string StudentNumber => Student?.StudentNumber ?? string.Empty;

    /// <summary>Gets the canonical class code.</summary>
    public string ClassCode => Student?.ClassIdentifier.CanonicalCode ?? string.Empty;

    /// <summary>Gets the readable historical resolution.</summary>
    public string Status => Student?.Status ?? string.Empty;

    /// <summary>Gets the typed historical resolution.</summary>
    public HistoricalResolutionType? ResolutionType => Student?.ResolutionType;

    /// <summary>Gets a value that indicates whether the student will be included.</summary>
    public bool IsIncluded => Student?.Plan.IsIncluded == true;

    /// <summary>Gets the prominent generation consequence.</summary>
    public string OutcomeTitle => ResolutionType == HistoricalResolutionType.Conflict
        ? "Este alumno no se incluirá en el documento"
        : "Se incluirá en el documento";

    /// <summary>Gets the explanation of the generation consequence.</summary>
    public string OutcomeDescription => ResolutionType switch
    {
        HistoricalResolutionType.Found =>
            "Se utilizarán las medidas recuperadas del histórico coincidente.",
        HistoricalResolutionType.NewStudent =>
            "Se generará una fila con el nombre del listado PDF y las demás columnas vacías.",
        HistoricalResolutionType.Conflict =>
            "No existe una asociación segura. Los candidatos se muestran solo para consulta.",
        _ => string.Empty,
    };

    /// <summary>Gets the one-based position among visible students.</summary>
    public int Position => Student is null ? 0 : IndexOfStudent(Student) + 1;

    /// <summary>Gets the number of students visible under current filters.</summary>
    public int VisibleCount => _visibleStudents.Count;

    /// <summary>Gets the contextual position text.</summary>
    public string PositionText => $"{Position} de {VisibleCount}";

    /// <summary>Gets a value that indicates whether previous-student navigation is available.</summary>
    public bool CanNavigatePrevious => Position > 1;

    /// <summary>Gets a value that indicates whether next-student navigation is available.</summary>
    public bool CanNavigateNext => Position > 0 && Position < VisibleCount;

    /// <summary>Gets the candidates available for conflict inspection.</summary>
    public IReadOnlyList<HistoricalStudentCandidate> Candidates =>
        Student?.Plan.Resolution.Candidates ?? [];

    /// <summary>Gets a value that indicates whether historical candidates can be inspected.</summary>
    public bool HasCandidates => ResolutionType == HistoricalResolutionType.Conflict
        && Candidates.Count > 0;

    /// <summary>Gets the candidate count label.</summary>
    public string CandidatesHeader => $"Históricos candidatos ({Candidates.Count})";

    /// <summary>Gets the current candidate position label.</summary>
    public string CandidatePositionText
    {
        get
        {
            var index = IndexOfCandidate(InspectedCandidate);
            return index < 0 ? string.Empty : $"Candidato en consulta {index + 1} de {Candidates.Count}";
        }
    }

    /// <summary>Gets a value that indicates whether a previous candidate exists.</summary>
    public bool CanNavigatePreviousCandidate => IndexOfCandidate(InspectedCandidate) > 0;

    /// <summary>Gets a value that indicates whether a next candidate exists.</summary>
    public bool CanNavigateNextCandidate
    {
        get
        {
            var index = IndexOfCandidate(InspectedCandidate);
            return index >= 0 && index < Candidates.Count - 1;
        }
    }

    /// <summary>Gets the continuous rich measures for the inspected historical record.</summary>
    public HistoricalRichContent MeasuresContent => CreateMeasuresContent(InspectedCandidate);

    /// <summary>Gets a value that indicates whether the current historical record has visible measures.</summary>
    public bool HasMeasures => MeasuresContent.HasContent;

    /// <summary>Gets the empty-state explanation for the measures viewer.</summary>
    public string MeasuresEmptyMessage => ResolutionType switch
    {
        HistoricalResolutionType.NewStudent => NoMeasuresMessage,
        HistoricalResolutionType.Conflict when Candidates.Count == 0 =>
            "No hay históricos candidatos que consultar",
        _ => NoMeasuresMessage,
    };

    /// <summary>Gets the traceable document sources for the current inspection.</summary>
    public IReadOnlyList<StudentSourceViewModel> Provenance
    {
        get
        {
            if (Student is null)
            {
                return [];
            }

            var sources = new List<StudentSourceViewModel>
            {
                new(
                    "Listado PDF",
                    Path.GetFileName(Student.RosterSourcePath),
                    Student.RosterSourcePath,
                    Student.Plan.RosterEntry.Location),
            };
            if (InspectedCandidate is not null)
            {
                sources.Add(new StudentSourceViewModel(
                    ResolutionType == HistoricalResolutionType.Found
                        ? "Histórico utilizado"
                        : "Histórico en consulta",
                    Path.GetFileName(InspectedCandidate.Record.DocumentPath),
                    InspectedCandidate.Record.DocumentPath,
                    InspectedCandidate.Record.Location));
            }

            return sources;
        }
    }

    /// <summary>Gets issues associated with the displayed PDF assignment.</summary>
    public IReadOnlyList<ReviewIssueViewModel> Issues => Student?.Issues ?? [];

    /// <summary>Gets the issue-section label.</summary>
    public string IssuesHeader => $"Incidencias ({Issues.Count})";

    /// <summary>Gets the latest controlled error from opening a source.</summary>
    public string? SourceOpenError
    {
        get => _sourceOpenError;
        private set
        {
            if (SetProperty(ref _sourceOpenError, value))
            {
                OnPropertyChanged(nameof(HasSourceOpenError));
            }
        }
    }

    /// <summary>Gets a value that indicates whether opening a source failed.</summary>
    public bool HasSourceOpenError => !string.IsNullOrWhiteSpace(SourceOpenError);

    /// <summary>Opens the detail for a student within the current visible result set.</summary>
    /// <param name="student">The student to display.</param>
    /// <param name="visibleStudents">The filtered students in navigation order.</param>
    public void Open(
        StudentReviewViewModel student,
        IReadOnlyList<StudentReviewViewModel> visibleStudents)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(visibleStudents);
        _visibleStudents = Array.AsReadOnly(visibleStudents.ToArray());
        Student = student;
        IsOpen = true;
        NotifyNavigationChanged();
    }

    /// <summary>Updates the filtered navigation set and closes when the student is excluded.</summary>
    /// <param name="visibleStudents">The filtered students in navigation order.</param>
    public void UpdateVisibleStudents(IReadOnlyList<StudentReviewViewModel> visibleStudents)
    {
        ArgumentNullException.ThrowIfNull(visibleStudents);
        _visibleStudents = Array.AsReadOnly(visibleStudents.ToArray());
        if (Student is not null && IndexOfStudent(Student) < 0)
        {
            Close();
            return;
        }

        NotifyNavigationChanged();
    }

    /// <summary>Shows another visible student without closing the panel.</summary>
    /// <param name="student">The student to display.</param>
    public void ShowStudent(StudentReviewViewModel student)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (IndexOfStudent(student) >= 0)
        {
            Student = student;
        }
    }

    /// <summary>Closes and clears the current detail.</summary>
    public void Close()
    {
        IsOpen = false;
        Student = null;
        _visibleStudents = [];
        SourceOpenError = null;
        NotifyNavigationChanged();
    }

    private static HistoricalStudentCandidate? InitialCandidate(StudentReviewViewModel? student) =>
        student?.ResolutionType switch
        {
            HistoricalResolutionType.Found => student.Plan.Resolution.SelectedCandidate,
            HistoricalResolutionType.Conflict when student.Plan.Resolution.Candidates.Count > 0 =>
                student.Plan.Resolution.Candidates[0],
            _ => null,
        };

    private void NavigateStudent(int offset)
    {
        if (Student is null)
        {
            return;
        }

        var index = IndexOfStudent(Student) + offset;
        if (index < 0 || index >= _visibleStudents.Count)
        {
            return;
        }

        Student = _visibleStudents[index];
        StudentChanged?.Invoke(this, new StudentDetailStudentChangedEventArgs(Student));
    }

    private void NavigateCandidate(int offset)
    {
        var index = IndexOfCandidate(InspectedCandidate) + offset;
        if (index >= 0 && index < Candidates.Count)
        {
            InspectedCandidate = Candidates[index];
        }
    }

    private void InspectCandidate(object? parameter)
    {
        if (parameter is HistoricalStudentCandidate candidate && Candidates.Contains(candidate))
        {
            InspectedCandidate = candidate;
        }
    }

    private void OpenSource(object? parameter)
    {
        if (parameter is not StudentSourceViewModel source)
        {
            return;
        }

        SourceOpenError = null;
        try
        {
            _sourceDocumentService.Open(source.Path);
        }
        catch (Exception error) when (error is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            SourceOpenError = $"No se pudo abrir el documento: {error.Message}";
        }
    }

    private void NotifyStudentChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(StudentNumber));
        OnPropertyChanged(nameof(ClassCode));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ResolutionType));
        OnPropertyChanged(nameof(IsIncluded));
        OnPropertyChanged(nameof(OutcomeTitle));
        OnPropertyChanged(nameof(OutcomeDescription));
        OnPropertyChanged(nameof(Candidates));
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(CandidatesHeader));
        OnPropertyChanged(nameof(Issues));
        OnPropertyChanged(nameof(IssuesHeader));
        OnPropertyChanged(nameof(Provenance));
        NotifyNavigationChanged();
    }

    private void NotifyNavigationChanged()
    {
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(CanNavigatePrevious));
        OnPropertyChanged(nameof(CanNavigateNext));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        CloseCommand.NotifyCanExecuteChanged();
        PreviousStudentCommand.NotifyCanExecuteChanged();
        NextStudentCommand.NotifyCanExecuteChanged();
        PreviousCandidateCommand.NotifyCanExecuteChanged();
        NextCandidateCommand.NotifyCanExecuteChanged();
    }

    private int IndexOfStudent(StudentReviewViewModel student)
    {
        for (var index = 0; index < _visibleStudents.Count; index++)
        {
            if (ReferenceEquals(_visibleStudents[index], student))
            {
                return index;
            }
        }

        return -1;
    }

    private int IndexOfCandidate(HistoricalStudentCandidate? candidate)
    {
        if (candidate is null)
        {
            return -1;
        }

        for (var index = 0; index < Candidates.Count; index++)
        {
            if (ReferenceEquals(Candidates[index], candidate) || Candidates[index] == candidate)
            {
                return index;
            }
        }

        return -1;
    }

    private static HistoricalRichContent CreateMeasuresContent(
        HistoricalStudentCandidate? candidate)
    {
        if (candidate is null)
        {
            return HistoricalRichContent.Empty;
        }

        var paragraphs = new List<HistoricalRichParagraph>();
        foreach (var field in candidate.ReviewFields.Where(HasVisibleContent))
        {
            if (field.PreviewContent.HasContent)
            {
                paragraphs.AddRange(field.PreviewContent.Paragraphs);
                continue;
            }

            paragraphs.Add(new HistoricalRichParagraph(
                [new HistoricalRichInline(
                    HistoricalRichInlineKind.Text,
                    field.Text,
                    HistoricalTextFormatting.Default)],
                HistoricalParagraphFormatting.Default));
        }

        return new HistoricalRichContent(paragraphs);
    }

    private static bool HasVisibleContent(HistoricalInformationField field) =>
        field.PreviewContent.HasContent
        || field.HasNonTextContent
        || !string.IsNullOrWhiteSpace(field.Text);
}

/// <summary>Identifies a source document shown in the integrated student detail.</summary>
/// <param name="Kind">The readable source kind.</param>
/// <param name="DisplayName">The source file name.</param>
/// <param name="Path">The absolute source path.</param>
/// <param name="Location">The readable location inside the source.</param>
public sealed record StudentSourceViewModel(
    string Kind,
    string DisplayName,
    string Path,
    string Location);

/// <summary>Provides data for a student changed through detail navigation.</summary>
/// <param name="student">The newly displayed student.</param>
public sealed class StudentDetailStudentChangedEventArgs(StudentReviewViewModel student) : EventArgs
{
    /// <summary>Gets the newly displayed student.</summary>
    public StudentReviewViewModel Student { get; } = student;
}
