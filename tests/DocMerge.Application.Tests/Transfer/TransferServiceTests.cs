using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Transfer;
using DocMerge.Application.Validation;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;
using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Tests.Transfer;

public sealed class TransferServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidationFailure_DoesNotPublishOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"docmerge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "output.docx");
            await File.WriteAllTextAsync(output, "SALIDA-ANTERIOR");
            var fileStore = new RecordingFileStore(root);
            var service = CreateService(fileStore, new FailingValidator());
            var request = Request(output);

            await Assert.ThrowsAsync<TransferValidationException>(() =>
                service.ExecuteAsync(request, null, CancellationToken.None));

            Assert.False(fileStore.WasPublished);
            Assert.Equal("SALIDA-ANTERIOR", await File.ReadAllTextAsync(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TransferService CreateService(
        IFileStore fileStore,
        ITransferResultValidator validator)
    {
        return new TransferService(
            new StubPdfReader(),
            new StubDocumentReader(),
            new StubEditor(),
            validator,
            fileStore,
            new TransferPlanner());
    }

    private static TransferExecutionRequest Request(string output)
    {
        var plan = new TransferPlan([], [], string.Empty, string.Empty, string.Empty, 0);
        return new TransferExecutionRequest(
            new TransferInputs("source.docx", "destination.docx", "list.pdf"),
            output,
            SortingProfiles.Get(SortingProfiles.DefaultIdentifier),
            plan);
    }

    private sealed class StubPdfReader : IPdfNameReader
    {
        public Task<PdfReadResult> ReadAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PdfReadResult([], 0, 0));
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
        public async Task<int> SortAsync(
            SortDocumentRequest request,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(request.OutputPath, "sorted", cancellationToken);
            return 0;
        }

        public async Task<int> ApplyTransferPlanAsync(
            ApplyTransferPlanRequest request,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(request.OutputPath, "transferred", cancellationToken);
            return 0;
        }
    }

    private sealed class FailingValidator : ITransferResultValidator
    {
        public Task ValidateAsync(
            TransferValidationRequest request,
            CancellationToken cancellationToken)
        {
            throw new TransferValidationException("fallo");
        }
    }

    private sealed class RecordingFileStore : IFileStore
    {
        private readonly string _root;

        internal RecordingFileStore(string root)
        {
            _root = root;
        }

        internal bool WasPublished { get; private set; }

        public Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }

        public string CreateTemporaryPath(string directory, string label)
        {
            return Path.Combine(_root, $"{label}.docx");
        }

        public Task PublishAsync(
            string temporaryPath,
            string outputPath,
            CancellationToken cancellationToken)
        {
            WasPublished = true;
            return Task.CompletedTask;
        }

        public void DeleteIfExists(string path)
        {
            File.Delete(path);
        }
    }
}
