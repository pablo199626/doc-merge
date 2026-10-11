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

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task AnalyzeCommand_ValidInputs_AdvancesToReadOnlyReview()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);

        await viewModel.AnalyzeCommand.ExecuteAsync();

        Assert.True(viewModel.IsReviewStep);
        Assert.Single(viewModel.Classes);
        Assert.Equal("Sin histórico", viewModel.VisibleStudents[0].Status);
        Assert.Equal("12345678", viewModel.VisibleStudents[0].StudentNumber);
    }

    [Fact]
    public async Task FilterReview_StatusFilter_DoesNotChangeBatchSummary()
    {
        using var fixture = new ViewModelFixture();
        var viewModel = fixture.CreateViewModel();
        fixture.SelectInputs(viewModel);
        await viewModel.AnalyzeCommand.ExecuteAsync();
        var summary = viewModel.BatchSummary;

        viewModel.SelectedFilter = "Recuperado";

        Assert.Empty(viewModel.VisibleStudents);
        Assert.Equal(summary, viewModel.BatchSummary);
        Assert.Equal(1, viewModel.SelectedClass!.StudentCount);
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

        viewModel.FilterByStatusCommand.Execute("Conflictos");

        Assert.Equal(3, viewModel.VisibleStudents.Count);
        Assert.Equal(7, selectedClass.StudentCount);
        Assert.Equal(3, selectedClass.RecoveredCount);
        Assert.Equal(1, selectedClass.MissingCount);
        Assert.Equal(3, selectedClass.UnresolvedCount);
        Assert.Equal(1, selectedClass.DocumentWarningCount);
        Assert.Equal(1, selectedClass.OtherWarningCount);
        Assert.True(viewModel.HasActiveReviewFilters);

        viewModel.FilterByStatusCommand.Execute("Conflictos");

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
        viewModel.SelectedFilter = "Sin histórico";
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

    private sealed class ViewModelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"docmerge-wpf-{Guid.NewGuid():N}");

        internal ViewModelFixture()
        {
            Directory.CreateDirectory(HistoricalDirectory);
            Directory.CreateDirectory(RosterDirectory);
            File.WriteAllText(TemplatePath, "template");
            File.WriteAllText(HistoricalPath, "historical");
            File.WriteAllText(RosterPath, "roster");
        }

        internal bool FailRosterRead { get; init; }

        internal bool UseMixedReview { get; init; }

        internal string HistoricalDirectory => Path.Combine(_root, "history");
        private string RosterDirectory => Path.Combine(_root, "rosters");
        private string TemplatePath => Path.Combine(_root, "template.docx");
        private string HistoricalPath => Path.Combine(HistoricalDirectory, "history.docx");
        private string RosterPath => Path.Combine(RosterDirectory, "2A.pdf");

        internal MainWindowViewModel CreateViewModel()
        {
            var inputs = new GenerationInputSet([HistoricalPath], [RosterPath]);
            var discovery = new StubDiscovery(inputs);
            var preparation = new ClassGenerationService(
                discovery,
                new StubRosterReader(RosterPath, FailRosterRead, UseMixedReview),
                new StubTemplateReader(TemplatePath),
                new StubHistoricalReader(HistoricalPath, UseMixedReview),
                new ClassGenerationPlanner(new HistoricalStudentResolver()));
            var execution = new ClassGenerationExecutionService(
                new StubGenerator(),
                new StubValidator(),
                new StubPublisher(),
                new StubFileStore(),
                discovery);
            return new MainWindowViewModel(
                preparation,
                execution,
                new StubDialogs(),
                new StubTheme(),
                new StubPreferences(),
                new StubFolderService());
        }

        internal void SelectInputs(MainWindowViewModel viewModel)
        {
            viewModel.SelectDroppedPath(InputKind.HistoricalDocuments, HistoricalDirectory);
            viewModel.SelectDroppedPath(InputKind.Rosters, RosterDirectory);
            viewModel.SelectDroppedPath(InputKind.Template, TemplatePath);
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

        private sealed class StubRosterReader(string path, bool fail, bool useMixedReview) : IClassRosterReader
        {
            public Task<ClassRoster> ReadAsync(string ignored, CancellationToken cancellationToken)
            {
                if (fail)
                {
                    throw new IOException("Roster read failed.");
                }

                var entries = useMixedReview
                    ? MixedEntries()
                    : [Entry("ALUMNO, UNO", "12345678", 1)];
                return Task.FromResult(new ClassRoster(
                    path, "roster-hash", new ClassIdentifier("ESO", 2, "A"), entries, 1, false));
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

        private sealed class StubTemplateReader(string path) : IGenerationTemplateReader
        {
            public Task<GenerationTemplateSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new GenerationTemplateSnapshot(
                    path, "template-hash", 0, 0, 1, ["NIA", header, "Información"], "<row />"));
        }

        private sealed class StubHistoricalReader(string path, bool useMixedReview) : IHistoricalStudentReader
        {
            public Task<HistoricalDocumentSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new HistoricalDocumentSnapshot(
                    path,
                    "historical-hash",
                    ["NIA", header, "Información"],
                    useMixedReview ? MixedCandidates(path) : []));

            private static HistoricalStudentCandidate[] MixedCandidates(string path) =>
            [
                Candidate(path, "ÚNICO, ALUMNO", "unique"),
                Candidate(path, "EQUIVALENTE, ALUMNO", "equivalent"),
                Candidate(path + ".copy", "EQUIVALENTE, ALUMNO", "equivalent"),
                Candidate(path, "CONFLICTO, ALUMNO", "first"),
                Candidate(path + ".copy", "CONFLICTO, ALUMNO", "second"),
                Candidate(path, "AMBIGUO, ALUMNO", "ambiguous"),
                Candidate(path, "OTRO, ÚNICO", "other"),
            ];

            private static HistoricalStudentCandidate Candidate(string documentPath, string name, string signature)
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
                return new HistoricalStudentCandidate(record, matchKey, signature);
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

        private sealed class StubPublisher : IGenerationBatchPublisher
        {
            public string CreateTemporaryDirectory(string outputDirectory) => outputDirectory + ".tmp";

            public Task PublishAsync(
                string temporaryDirectory,
                string outputDirectory,
                CancellationToken cancellationToken) => Task.CompletedTask;

            public void DeleteIfExists(string temporaryDirectory)
            {
            }
        }

        private sealed class StubFileStore : IFileStore
        {
            public string GetFullPath(string path) => path;
            public Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult(string.Empty);
            public string CreateTemporaryPath(string outputPath, string label) => outputPath + ".tmp";
            public Task PublishAsync(string temporaryPath, string outputPath, CancellationToken cancellationToken) =>
                Task.CompletedTask;
            public void DeleteIfExists(string path)
            {
            }
        }

        private sealed class StubDialogs : IFileDialogService
        {
            public string? SelectFolder(string title, string? initialDirectory = null) => null;
            public string? SelectDocx(string title, string? initialDirectory = null) => null;
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
