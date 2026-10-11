using System.IO;
using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Application.Pdf;
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

        private string HistoricalDirectory => Path.Combine(_root, "history");
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
                new StubRosterReader(RosterPath),
                new StubTemplateReader(TemplatePath),
                new StubHistoricalReader(HistoricalPath),
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

        private sealed class StubRosterReader(string path) : IClassRosterReader
        {
            public Task<ClassRoster> ReadAsync(string ignored, CancellationToken cancellationToken)
            {
                var entry = new ClassRosterEntry(
                    "ALUMNO, UNO", "alumno, uno", "alumno uno", "12345678", 1, 1);
                return Task.FromResult(new ClassRoster(
                    path, "roster-hash", new ClassIdentifier("ESO", 2, "A"), [entry], 1, false));
            }
        }

        private sealed class StubTemplateReader(string path) : IGenerationTemplateReader
        {
            public Task<GenerationTemplateSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new GenerationTemplateSnapshot(
                    path, "template-hash", 0, 0, 1, ["NIA", header, "Información"], "<row />"));
        }

        private sealed class StubHistoricalReader(string path) : IHistoricalStudentReader
        {
            public Task<HistoricalDocumentSnapshot> ReadAsync(
                string ignored,
                string header,
                CancellationToken cancellationToken) => Task.FromResult(new HistoricalDocumentSnapshot(
                    path, "historical-hash", ["NIA", header, "Información"], []));
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
            public bool IsDark { get; private set; }
            public void ApplySavedTheme()
            {
            }
            public void SetDark(bool isDark) => IsDark = isDark;
        }

        private sealed class StubFolderService : ISystemFolderService
        {
            public void Open(string path)
            {
            }
        }
    }
}
