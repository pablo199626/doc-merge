using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Sorting;
using DocMerge.Application.Transfer;
using DocMerge.Application.Validation;
using DocMerge.Console.Cli;
using DocMerge.Console.IO;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;
using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Console.Tests.Cli;

public sealed class ConsoleApplicationTests
{
    [Fact]
    public async Task RunAsync_NoArguments_RunsTransferSimulationWizardAndPauses()
    {
        var root = Path.Combine(Path.GetTempPath(), $"docmerge_cli_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = CreateFile(root, "source.docx");
            var destination = CreateFile(root, "destination.docx");
            var list = CreateFile(root, "list.pdf");
            var console = new FakeConsole(["1", source, destination, list, "", "s", ""]);
            var application = CreateApplication(console);

            var exitCode = await application.RunAsync([], CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Contains(console.Output, line => line.Contains(
                "Transferir personas", StringComparison.Ordinal));
            Assert.Contains(console.Output, line => line.Contains(
                "Simulación de transferencia", StringComparison.Ordinal));
            Assert.Contains(console.Prompts, prompt => prompt.Contains(
                "Pulsa Intro para cerrar", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ConsoleApplication CreateApplication(IConsoleIO console)
    {
        var editor = new StubEditor();
        var reader = new StubDocumentReader();
        var transferService = new TransferService(
            new StubPdfReader(), reader, editor, new StubValidator(),
            new StubFileStore(), new TransferPlanner());
        return new ConsoleApplication(
            new CliParser(), console, new SortDocumentService(editor), transferService);
    }

    private static string CreateFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, "placeholder");
        return path;
    }

    private sealed class FakeConsole : IConsoleIO
    {
        private readonly Queue<string> _answers;

        internal FakeConsole(IEnumerable<string> answers)
        {
            _answers = new Queue<string>(answers);
        }

        internal List<string> Output { get; } = [];

        internal List<string> Prompts { get; } = [];

        public void WriteLine(string value = "") => Output.Add(value);

        public void WriteError(string value) => Output.Add(value);

        public string ReadLine(string prompt)
        {
            Prompts.Add(prompt);
            return _answers.Dequeue();
        }
    }

    private sealed class StubPdfReader : IPdfNameReader
    {
        public Task<PdfReadResult> ReadAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PdfReadResult([], 0, 1));
        }
    }

    private sealed class StubDocumentReader : IDocumentReader
    {
        public Task<DocumentSnapshot> ReadAsync(
            string path,
            string header,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new DocumentSnapshot(path, string.Empty, 0, 0, 1, [], []));
        }
    }

    private sealed class StubEditor : IDocumentEditor
    {
        public Task<int> SortAsync(
            SortDocumentRequest request,
            CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> ApplyTransferPlanAsync(
            ApplyTransferPlanRequest request,
            CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class StubValidator : ITransferResultValidator
    {
        public Task ValidateAsync(
            TransferValidationRequest request,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubFileStore : IFileStore
    {
        public Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public string CreateTemporaryPath(string directory, string label) =>
            Path.Combine(directory, $"{label}.docx");

        public Task PublishAsync(
            string temporaryPath,
            string outputPath,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public void DeleteIfExists(string path)
        {
        }
    }
}
