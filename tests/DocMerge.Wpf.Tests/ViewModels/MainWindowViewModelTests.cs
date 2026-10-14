using System.IO;
using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.Services;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public void Version_DesktopAssembly_UsesCommercialVersion()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();

        Assert.Equal("1.0.0", viewModel.Version);
    }

    [Fact]
    public async Task AnalyzeCommand_ValidInputs_AdvancesToReadOnlyReview()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);

        await viewModel.AnalyzeCommand.ExecuteAsync();

        Assert.True(viewModel.IsReviewStep);
        Assert.Single(viewModel.Classes);
        Assert.Equal("Alumno nuevo", viewModel.VisibleStudents[0].Status);
        Assert.Equal("12345678", viewModel.VisibleStudents[0].StudentNumber);
        Assert.Equal(
            ["Todos los estados", "Encontrado", "Alumno nuevo", "Conflicto"],
            viewModel.StatusFilters);
    }

    [Fact]
    public async Task AnalyzeCommand_AllStudentsConflicted_DisablesConfigurationAndExplainsEmptyOutput()
    {
        using var fixture = new ViewModelFixture { AllStudentsConflict = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);

        await viewModel.AnalyzeCommand.ExecuteAsync();

        Assert.Equal(DesktopWorkflowState.Reviewing, viewModel.WorkflowState);
        Assert.Equal("Conflicto", Assert.Single(viewModel.VisibleStudents).Status);
        Assert.False(viewModel.CanContinueToGeneration);
        Assert.Empty(viewModel.ClassHeaders);
        Assert.Equal("No hay alumnos sin conflicto para generar documentos.", viewModel.ReviewReadinessMessage);
    }

    [Fact]
    public async Task ContinueToGenerationCommand_PreparedReview_AdvancesWithoutPublishing()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();

        viewModel.ContinueToGenerationCommand.Execute(parameter: null);

        Assert.True(viewModel.IsGenerationStep);
        Assert.Null(viewModel.PublishedDirectory);
        Assert.Null(viewModel.FinalStatus);
    }

    [Fact]
    public async Task BackToFilesCommand_UnchangedInputs_DiscardsReviewAndReanalyzes()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var originalClass = Assert.Single(viewModel.Classes);

        viewModel.BackToFilesCommand.Execute(parameter: null);
        fixture.RosterStudentName = "ALUMNO, DOS";
        await viewModel.AnalyzeCommand.ExecuteAsync();

        Assert.NotSame(originalClass, Assert.Single(viewModel.Classes));
        Assert.Equal("ALUMNO, DOS", Assert.Single(viewModel.VisibleStudents).Name);
        Assert.Equal(2, fixture.RosterReadCount);
        Assert.Equal(DesktopWorkflowState.Reviewing, viewModel.WorkflowState);
    }

    [Fact]
    public async Task AnalyzeCommand_SecondAnalysisCancelled_DoesNotRestorePreviousReview()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.BackToFilesCommand.Execute(parameter: null);
        fixture.BlockRosterRead = true;

        var analysis = viewModel.AnalyzeCommand.ExecuteAsync();
        await fixture.RosterReadStarted.Task;
        viewModel.CancelOperation();
        await analysis;

        Assert.Equal(DesktopWorkflowState.AnalysisCancelled, viewModel.WorkflowState);
        Assert.Empty(viewModel.Classes);
        Assert.Empty(viewModel.Issues);
        Assert.Empty(viewModel.ClassHeaders);
        Assert.False(viewModel.CanContinueToGeneration);
    }

    [Fact]
    public async Task BackToFilesCommand_PreparedWorkflow_ResetsDerivedStateAndKeepsInputs()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var historicalPath = viewModel.HistoricalInput.Path;
        var rosterPath = viewModel.RosterInput.Path;
        var templatePath = viewModel.TemplateInput.Path;
        viewModel.SearchText = "alumno";
        viewModel.SelectedFilter = "Alumno nuevo";
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);
        Assert.Single(viewModel.ClassHeaders).DisplayTitle = "Título anterior";
        viewModel.OutputFolderName = "Salida anterior";
        viewModel.BrowseOutputCommand.Execute(parameter: null);
        viewModel.BackToReviewCommand.Execute(parameter: null);

        viewModel.BackToFilesCommand.Execute(parameter: null);

        Assert.Equal(DesktopWorkflowState.Selecting, viewModel.WorkflowState);
        Assert.Equal(historicalPath, viewModel.HistoricalInput.Path);
        Assert.Equal(rosterPath, viewModel.RosterInput.Path);
        Assert.Equal(templatePath, viewModel.TemplateInput.Path);
        Assert.All(
            new[] { viewModel.HistoricalInput, viewModel.RosterInput, viewModel.TemplateInput },
            input => Assert.Equal(InputSelectionState.Selected, input.State));
        Assert.Empty(viewModel.Classes);
        Assert.Empty(viewModel.Issues);
        Assert.Empty(viewModel.ClassHeaders);
        Assert.Empty(viewModel.VisibleStudents);
        Assert.Null(viewModel.SelectedClass);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("Todos los estados", viewModel.SelectedFilter);
        Assert.Null(viewModel.OutputParent);
        Assert.Equal("Clases generadas", viewModel.OutputFolderName);
        Assert.False(viewModel.CanContinueToGeneration);
    }

    [Fact]
    public async Task GenerateCommand_ValidConfiguration_CreatesConfirmedImmutableResult()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);
        var header = Assert.Single(viewModel.ClassHeaders);
        header.DisplayTitle = "Segundo A";
        header.TutorName = "María Pérez";
        viewModel.BrowseOutputCommand.Execute(parameter: null);

        await viewModel.GenerateCommand.ExecuteAsync();

        Assert.Equal(DesktopWorkflowState.Completed, viewModel.WorkflowState);
        Assert.True(viewModel.IsGenerationCompleted);
        Assert.False(viewModel.IsConfigurationVisible);
        Assert.Equal("Segundo A", viewModel.GenerationResult!.Headers.Single().Value.DisplayTitle);
        Assert.Equal("María Pérez", viewModel.GenerationResult.Headers.Single().Value.TutorName);
        Assert.Equal(1, fixture.PublishCount);
    }

    [Fact]
    public async Task NewGenerationCommand_CompletedResult_ResetsSessionAndRestoresBundledTemplate()
    {
        using var fixture = new ViewModelFixture
        {
            TemplateSupportsHeader = true,
            BlockBundledTemplate = true,
        };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);
        viewModel.BrowseOutputCommand.Execute(parameter: null);
        await viewModel.GenerateCommand.ExecuteAsync();
        viewModel.SearchText = "old";
        var previousSession = viewModel.SessionSequence;

        var reset = viewModel.NewGenerationCommand.ExecuteAsync();
        await fixture.BundledTemplateRequested.Task;
        var safeCloseWait = viewModel.WaitForActiveOperationAsync();
        Assert.False(safeCloseWait.IsCompleted);
        fixture.AllowBundledTemplate.TrySetResult();
        await reset;
        await safeCloseWait;

        Assert.Equal(previousSession + 1, viewModel.SessionSequence);
        Assert.Equal(DesktopWorkflowState.Selecting, viewModel.WorkflowState);
        Assert.False(viewModel.HistoricalInput.HasSelection);
        Assert.False(viewModel.RosterInput.HasSelection);
        Assert.True(viewModel.TemplateInput.IsBundled);
        Assert.Equal("Incluida con Talos", viewModel.TemplateInput.DisplayLocation);
        Assert.Null(viewModel.GenerationResult);
        Assert.Null(viewModel.OutputParent);
        Assert.Equal("Clases generadas", viewModel.OutputFolderName);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Empty(viewModel.ClassHeaders);
    }

    [Fact]
    public async Task ContinueToGenerationCommand_HeaderTemplate_CreatesEditableDefaultHeader()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();

        viewModel.ContinueToGenerationCommand.Execute(parameter: null);

        var header = Assert.Single(viewModel.ClassHeaders);
        Assert.True(header.IsEditable);
        Assert.Equal("2º ESO A", header.DisplayTitle);
        Assert.Equal(string.Empty, header.TutorName);
    }

    [Fact]
    public async Task GenerationHeader_NavigateBackAndForward_PreservesUserValues()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);
        var header = Assert.Single(viewModel.ClassHeaders);
        header.DisplayTitle = "Segundo C — apoyo";
        header.TutorName = "María Pérez";

        viewModel.BackToReviewCommand.Execute(parameter: null);
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);

        Assert.Same(header, Assert.Single(viewModel.ClassHeaders));
        Assert.Equal("Segundo C — apoyo", header.DisplayTitle);
        Assert.Equal("María Pérez", header.TutorName);
    }

    [Fact]
    public async Task GenerationHeader_InvalidTitle_DisablesGenerationState()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var header = Assert.Single(viewModel.ClassHeaders);

        header.DisplayTitle = "   ";

        Assert.True(header.HasErrors);
        Assert.True(viewModel.HasHeaderErrors);
        Assert.Contains("no puede estar vacío", header.TitleError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectInput_AfterHeaderEditing_InvalidatesPlanAndHeaders()
    {
        using var fixture = new ViewModelFixture { TemplateSupportsHeader = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);

        viewModel.SelectDroppedPath(InputKind.Template, fixture.SecondTemplatePath);

        Assert.True(viewModel.IsSelectionStep);
        Assert.Empty(viewModel.ClassHeaders);
        Assert.False(viewModel.CanContinueToGeneration);
    }

    [Fact]
    public async Task FilterReview_StatusFilter_DoesNotChangeBatchSummary()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var summary = viewModel.BatchSummary;

        viewModel.SelectedFilter = "Encontrado";

        Assert.Empty(viewModel.VisibleStudents);
        Assert.Equal(summary, viewModel.BatchSummary);
        Assert.Equal(1, viewModel.SelectedClass!.StudentCount);
    }

    [Fact]
    public async Task OpenStudentDetail_FilterExcludesStudent_ClosesWithoutChangingFilter()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var student = Assert.Single(viewModel.VisibleStudents);
        viewModel.OpenStudentDetailCommand.Execute(student);

        viewModel.SelectedFilter = "Encontrado";

        Assert.False(viewModel.IsStudentDetailOpen);
        Assert.Equal("Encontrado", viewModel.SelectedFilter);
        Assert.Empty(viewModel.VisibleStudents);
    }

    [Fact]
    public async Task ContinueToGenerationCommand_OpenDetail_ClosesDetailWithoutChangingPlan()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var summary = viewModel.BatchSummary;
        viewModel.OpenStudentDetailCommand.Execute(Assert.Single(viewModel.VisibleStudents));

        viewModel.ContinueToGenerationCommand.Execute(parameter: null);

        Assert.False(viewModel.IsStudentDetailOpen);
        Assert.True(viewModel.IsGenerationStep);
        Assert.Equal(summary, viewModel.BatchSummary);
    }

    [Fact]
    public async Task UpdateWindowWidth_OpenDetail_CompactsOnlyClassSelector()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.OpenStudentDetailCommand.Execute(Assert.Single(viewModel.VisibleStudents));

        viewModel.UpdateWindowWidth(1500);

        Assert.True(viewModel.IsStudentDetailOpen);
        Assert.False(viewModel.IsCompactReviewLayout);

        viewModel.UpdateWindowWidth(1100);

        Assert.True(viewModel.IsStudentDetailOpen);
        Assert.True(viewModel.IsCompactReviewLayout);
    }

    [Fact]
    public async Task AppearanceChanges_PreparedReview_RemainsAvailable()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var summary = viewModel.BatchSummary;

        viewModel.SetThemeCommand.Execute(ThemePreference.Dark);
        viewModel.ToggleSidebarCommand.Execute(parameter: null);

        Assert.True(viewModel.IsReviewStep);
        Assert.Equal(summary, viewModel.BatchSummary);
        Assert.Equal(ThemePreference.Dark, viewModel.SelectedThemePreference);
        Assert.True(viewModel.IsSidebarExpanded);
    }

    [Fact]
    public void SetThemeCommand_ExplicitAndSystemPreferences_UpdatePresentationImmediately()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();

        viewModel.SetThemeCommand.Execute(ThemePreference.Dark);

        Assert.True(viewModel.IsDarkThemeSelected);
        Assert.False(viewModel.IsSystemThemeSelected);
        Assert.Equal("Oscuro", viewModel.ThemeSummary);

        viewModel.SetThemeCommand.Execute(ThemePreference.System);

        Assert.True(viewModel.IsSystemThemeSelected);
        Assert.False(viewModel.IsDarkThemeSelected);
        Assert.Equal("Sistema · claro", viewModel.ThemeSummary);
    }

    [Fact]
    public async Task FilterReview_AggregateBadges_FilterWithoutChangingClassTotals()
    {
        using var fixture = new ViewModelFixture { UseMixedReview = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var selectedClass = Assert.IsType<ClassReviewViewModel>(viewModel.SelectedClass);

        viewModel.FilterByStatusCommand.Execute("Conflicto");

        Assert.Equal(4, viewModel.VisibleStudents.Count);
        Assert.Equal(7, selectedClass.StudentCount);
        Assert.Equal(2, selectedClass.FoundCount);
        Assert.Equal(1, selectedClass.NewStudentCount);
        Assert.Equal(4, selectedClass.ConflictCount);
        Assert.Equal(3, selectedClass.IncludedCount);
        Assert.True(viewModel.HasActiveReviewFilters);

        viewModel.FilterByStatusCommand.Execute("Conflicto");

        Assert.Equal(7, viewModel.VisibleStudents.Count);
        Assert.False(viewModel.HasActiveReviewFilters);
    }

    [Fact]
    public async Task ClearReviewFilters_SearchAndStatus_RestoresAllStudents()
    {
        using var fixture = new ViewModelFixture { UseMixedReview = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();

        viewModel.SearchText = "AUSENTE";
        viewModel.SelectedFilter = "Alumno nuevo";
        viewModel.ClearReviewFiltersCommand.Execute(parameter: null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("Todos los estados", viewModel.SelectedFilter);
        Assert.Equal(7, viewModel.VisibleStudents.Count);
    }

    [Fact]
    public void UpdateWindowWidth_CompactOverlay_PreservesExplicitSidebarPreference()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        viewModel.IsSidebarExpanded = true;

        viewModel.UpdateWindowWidth(800);

        Assert.True(viewModel.IsSidebarExpanded);
        Assert.True(viewModel.IsSidebarOverlayMode);
        Assert.True(viewModel.IsSidebarOverlayVisible);
        Assert.Equal(64, viewModel.SidebarColumnWidth);
    }

    [Fact]
    public void SelectDroppedPath_RejectedReplacement_KeepsPreviousSelection()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        var originalPath = viewModel.TemplateInput.Path;

        viewModel.SelectDroppedPath(InputKind.Template, fixture.HistoricalDirectory);

        Assert.Equal(originalPath, viewModel.TemplateInput.Path);
        Assert.True(viewModel.TemplateInput.HasError);
        Assert.Contains("conserva la selección anterior", viewModel.TemplateInput.Error);
    }

    [Fact]
    public async Task AnalyzeCommand_InputFailure_ClearsAnalyzingStateFromEveryCard()
    {
        using var fixture = new ViewModelFixture { FailRosterRead = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);

        await viewModel.AnalyzeCommand.ExecuteAsync();

        Assert.All(
            new[] { viewModel.HistoricalInput, viewModel.RosterInput, viewModel.TemplateInput },
            input => Assert.False(input.IsBusy));
        Assert.DoesNotContain(
            new[] { viewModel.HistoricalInput, viewModel.RosterInput, viewModel.TemplateInput },
            input => input.State == InputSelectionState.Analyzing);
        Assert.Equal(InputSelectionState.Error, viewModel.RosterInput.State);
    }

    [Fact]
    public async Task CancelOperation_AnalysisInProgress_RequestsOnceAndKeepsBundledTemplateIdentity()
    {
        using var fixture = new ViewModelFixture { BlockRosterRead = true };
        var viewModel = fixture.CreateViewModel();
        await viewModel.InitializeAsync();
        fixture.SelectSourceFolders(viewModel);
        var analysis = viewModel.AnalyzeCommand.ExecuteAsync();
        await fixture.RosterReadStarted.Task;

        viewModel.CancelOperation();
        viewModel.CancelOperation();
        await analysis;

        Assert.Equal(DesktopWorkflowState.AnalysisCancelled, viewModel.WorkflowState);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.TemplateInput.IsBundled);
        Assert.Equal("Incluida con Talos", viewModel.TemplateInput.DisplayLocation);
        Assert.Equal(InputSelectionState.Selected, viewModel.TemplateInput.State);
    }

    [Fact]
    public async Task CancelOperation_PublishingStarted_DoesNotTurnPublishedResultIntoCancellation()
    {
        using var fixture = new ViewModelFixture { BlockPublishing = true };
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        viewModel.ContinueToGenerationCommand.Execute(parameter: null);
        viewModel.BrowseOutputCommand.Execute(parameter: null);
        var generation = viewModel.GenerateCommand.ExecuteAsync();
        await fixture.PublishStarted.Task;
        await viewModel.GenerateCommand.ExecuteAsync();

        Assert.Equal(DesktopWorkflowState.Publishing, viewModel.WorkflowState);
        Assert.False(viewModel.CanCancelOperation);
        Assert.Equal(1, fixture.PublishCount);
        viewModel.CancelOperation();
        fixture.AllowPublishing.TrySetResult();
        await generation;

        Assert.Equal(DesktopWorkflowState.Completed, viewModel.WorkflowState);
        Assert.NotNull(viewModel.GenerationResult);
    }

    private sealed class ViewModelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"docmerge-wpf-{Guid.NewGuid():N}");

        internal ViewModelFixture()
        {
            Directory.CreateDirectory(HistoricalDirectory);
            Directory.CreateDirectory(RosterDirectory);
            File.WriteAllText(TemplatePath, "template");
            File.WriteAllText(SecondTemplatePath, "template-2");
            File.WriteAllText(HistoricalPath, "historical");
            File.WriteAllText(RosterPath, "roster");
        }

        internal bool FailRosterRead { get; init; }

        internal bool UseMixedReview { get; init; }

        internal bool TemplateSupportsHeader { get; init; }
        internal bool AllStudentsConflict { get; init; }
        internal bool BlockRosterRead { get; set; }
        internal bool BlockPublishing { get; init; }
        internal bool BlockBundledTemplate { get; init; }
        internal string RosterStudentName { get; set; } = "ALUMNO, UNO";
        internal int PublishCount { get; private set; }
        internal int RosterReadCount { get; private set; }
        internal TaskCompletionSource RosterReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PublishStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AllowPublishing { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource BundledTemplateRequested { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AllowBundledTemplate { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal string HistoricalDirectory => Path.Combine(_root, "history");
        private string RosterDirectory => Path.Combine(_root, "rosters");
        private string TemplatePath => Path.Combine(_root, "template.docx");
        internal string SecondTemplatePath => Path.Combine(_root, "template-2.docx");
        private string HistoricalPath => Path.Combine(HistoricalDirectory, "history.docx");
        private string RosterPath => Path.Combine(RosterDirectory, "2A.pdf");

        internal MainWindowViewModel CreateViewModel()
        {
            var inputs = new GenerationInputSet([HistoricalPath], [RosterPath]);
            var discovery = new StubDiscovery(inputs);
            var preparation = new ClassGenerationService(
                discovery,
                new StubRosterReader(this, RosterPath, FailRosterRead, UseMixedReview),
                new StubTemplateReader(TemplatePath, TemplateSupportsHeader),
                new StubHistoricalReader(HistoricalPath, UseMixedReview, AllStudentsConflict, IncludeRichMeasures),
                new ClassGenerationPlanner(new HistoricalStudentResolver()));
            var execution = new ClassGenerationExecutionService(
                new StubGenerator(),
                new StubValidator(),
                new StubPublisher(this),
                new StubFileStore(),
                discovery);
            return new MainWindowViewModel(
                preparation,
                execution,
                new StubDialogs(_root),
                new StubTheme(),
                new StubPreferences(),
                new StubFolderService(),
                new StubBundledTemplateProvider(this, TemplatePath));
        }

        internal void SelectInputs(MainWindowViewModel viewModel)
        {
            SelectSourceFolders(viewModel);
            viewModel.SelectDroppedPath(InputKind.Template, TemplatePath);
        }

        internal bool IncludeRichMeasures { get; init; }

        internal void SelectSourceFolders(MainWindowViewModel viewModel)
        {
            viewModel.SelectDroppedPath(InputKind.HistoricalDocuments, HistoricalDirectory);
            viewModel.SelectDroppedPath(InputKind.Rosters, RosterDirectory);
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private sealed class StubDiscovery(GenerationInputSet inputs) : IGenerationInputDiscovery
        {
            public Task<GenerationInputSet> DiscoverAsync(
                string historicalDirectory,
                string rosterDirectory,
                string templatePath,
                CancellationToken cancellationToken) => Task.FromResult(inputs);
        }

        private sealed class StubRosterReader(
            ViewModelFixture fixture,
            string path,
            bool fail,
            bool useMixedReview) : IClassRosterReader
        {
            public async Task<ClassRoster> ReadAsync(string ignored, CancellationToken cancellationToken)
            {
                fixture.RosterReadCount++;
                if (fail)
                {
                    throw new IOException("Roster read failed.");
                }

                if (fixture.BlockRosterRead)
                {
                    fixture.RosterReadStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                var entries = useMixedReview
                    ? MixedEntries()
                    : [Entry(fixture.RosterStudentName, "12345678", 1)];
                return new ClassRoster(
                    path, "roster-hash", new ClassIdentifier("ESO", 2, "A"), entries, 1, false);
            }

            private static ClassRosterEntry[] MixedEntries() =>
            [
                Entry("ÚNICO, ALUMNO", "10000001", 1),
                Entry("EQUIVALENTE, ALUMNO", "10000002", 2),
                Entry("AUSENTE, ALUMNO", "10000003", 3),
                Entry("CONFLICTO, ALUMNO", "10000004", 4),
                Entry("AMBIGUO, ALUMNO", "10000005", 5),
                Entry("AMBIGUO ALUMNO", "10000006", 6),
                Entry("OTRO, ÚNICO", "10000007", 7),
            ];

            private static ClassRosterEntry Entry(string name, string number, int ordinal) => new(
                name,
                name,
                StudentNameMatchKey.Create(name),
                number,
                1,
                ordinal);
        }

        private sealed class StubTemplateReader(string path, bool supportsHeader) : IGenerationTemplateReader
        {
            public Task<GenerationTemplateSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new GenerationTemplateSnapshot(
                    path,
                    "template-hash",
                    0,
                    0,
                    1,
                    ["NIA", header, "Información"],
                    "<row />",
                    supportsHeader ? 1 : null));
        }

        private sealed class StubHistoricalReader(
            string path,
            bool useMixedReview,
            bool allStudentsConflict,
            bool includeRichMeasures) : IHistoricalStudentReader
        {
            public Task<HistoricalDocumentSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new HistoricalDocumentSnapshot(
                    path,
                    "historical-hash",
                    ["NIA", header, "Información"],
                    allStudentsConflict
                        ? [Candidate(path, "ALUMNO, UNO"), Candidate(path + ".copy", "ALUMNO, UNO")]
                        : useMixedReview ? MixedCandidates(path) : []));

            private HistoricalStudentCandidate[] MixedCandidates(string path) =>
            [
                Candidate(path, "ÚNICO, ALUMNO"),
                Candidate(path, "EQUIVALENTE, ALUMNO"),
                Candidate(path + ".copy", "EQUIVALENTE, ALUMNO"),
                Candidate(path, "CONFLICTO, ALUMNO"),
                Candidate(path + ".copy", "CONFLICTO, ALUMNO"),
                Candidate(path, "AMBIGUO, ALUMNO"),
                Candidate(path, "OTRO, ÚNICO"),
            ];

            private HistoricalStudentCandidate Candidate(string documentPath, string name)
            {
                var matchKey = StudentNameMatchKey.Create(name);
                var record = new DocumentRecord(
                    documentPath,
                    0,
                    1,
                    name,
                    name,
                    "<row />",
                    new RecordDependencies([], [], []));
                if (!includeRichMeasures)
                {
                    return new HistoricalStudentCandidate(record, matchKey);
                }

                var content = new HistoricalRichContent(Enumerable.Range(1, 18).Select(index =>
                    new HistoricalRichParagraph(
                        [new HistoricalRichInline(HistoricalRichInlineKind.Text,
                            $"Medida {index}: facilitar instrucciones claras, materiales accesibles y tiempo suficiente para revisar las actividades. Mantener seguimiento individual y coordinación con la familia.",
                            new HistoricalTextFormatting(IsBold: index == 1))],
                        new HistoricalParagraphFormatting(SpaceAfterPoints: 8))).ToArray());
                return new HistoricalStudentCandidate(record, matchKey,
                    [new HistoricalInformationField("Medidas", "Medidas extensas", false, content)]);
            }
        }

        private sealed class StubGenerator : IClassDocumentGenerator
        {
            public Task<IReadOnlyList<GeneratedClassDocument>> GenerateAsync(
                GenerateClassDocumentsRequest request,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<GeneratedClassDocument>>([]);
        }

        private sealed class StubValidator : IClassGenerationValidator
        {
            public Task ValidateAsync(
                ValidateClassDocumentsRequest request,
                CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class StubPublisher(ViewModelFixture fixture) : IGenerationBatchPublisher
        {
            public string CreateTemporaryDirectory(string outputDirectory) => outputDirectory + ".tmp";

            public async Task PublishAsync(
                string temporaryDirectory,
                string outputDirectory,
                CancellationToken cancellationToken)
            {
                fixture.PublishCount++;
                if (!fixture.BlockPublishing)
                {
                    return;
                }

                fixture.PublishStarted.TrySetResult();
                await fixture.AllowPublishing.Task;
            }

            public void DeleteIfExists(string temporaryDirectory)
            {
            }
        }

        private sealed class StubFileStore : IFileStore
        {
            public string GetFullPath(string path) => path;
            public Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult(Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? "roster-hash"
                    : Path.GetFileName(path).StartsWith("template", StringComparison.OrdinalIgnoreCase)
                        ? "template-hash"
                        : "historical-hash");
            public string CreateTemporaryPath(string outputPath, string label) => outputPath + ".tmp";
            public Task PublishAsync(string temporaryPath, string outputPath, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public void DeleteIfExists(string path)
            {
            }
        }

        private sealed class StubDialogs(string outputParent) : IFileDialogService
        {
            public string? SelectFolder(string title, string? initialDirectory = null) => outputParent;
            public string? SelectDocx(string title, string? initialDirectory = null) => null;
        }

        private sealed class StubBundledTemplateProvider(ViewModelFixture fixture, string path)
            : IBundledGenerationTemplateProvider
        {
            public async Task<BundledGenerationTemplate> GetAsync(CancellationToken cancellationToken)
            {
                if (fixture.BlockBundledTemplate)
                {
                    fixture.BundledTemplateRequested.TrySetResult();
                    await fixture.AllowBundledTemplate.Task;
                }

                return new BundledGenerationTemplate(
                    "standard-class",
                    1,
                    "docx",
                    1,
                    "Plantilla estándar de DocMerge",
                    "hash",
                    path);
            }
        }

        private sealed class StubTheme : IThemeService
        {
            public event EventHandler? Changed;
            public ThemePreference Preference { get; private set; }
            public bool IsDark { get; private set; }
            public void ApplySavedTheme()
            {
            }

            public void SetPreference(ThemePreference preference)
            {
                Preference = preference;
                IsDark = preference == ThemePreference.Dark;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class StubPreferences : IDesktopPreferencesService
        {
            private DesktopPreferences _preferences = DesktopPreferences.Default;

            public DesktopPreferences Load() => _preferences;

            public void SetThemePreference(ThemePreference preference) =>
                _preferences = _preferences with { ThemePreference = preference };

            public void SetSidebarExpanded(bool isExpanded) =>
                _preferences = _preferences with { IsSidebarExpanded = isExpanded };
        }

        private sealed class StubFolderService : ISystemFolderService
        {
            public void Open(string path)
            {
            }
        }
    }
}
