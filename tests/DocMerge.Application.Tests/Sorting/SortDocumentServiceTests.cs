using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Sorting;
using DocMerge.Application.Validation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Tests.Sorting;

public sealed class SortDocumentServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_DelegatesToEditor()
    {
        var editor = new RecordingEditor();
        var service = new SortDocumentService(editor, new StubFileStore());
        var request = CreateRequest("input.docx", "output.docx");

        var count = await service.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(3, count);
        Assert.Same(request, editor.SortRequest);
    }

    [Fact]
    public async Task ExecuteAsync_OutputMatchesInput_RejectsRequest()
    {
        var editor = new RecordingEditor();
        var service = new SortDocumentService(editor, new StubFileStore());
        var request = CreateRequest("document.docx", ".\\document.docx");

        var error = await Assert.ThrowsAsync<OperationValidationException>(() =>
            service.ExecuteAsync(request, CancellationToken.None));

        Assert.Contains("distinto", error.Message, StringComparison.Ordinal);
        Assert.Null(editor.SortRequest);
    }

    [Fact]
    public async Task ExecuteAsync_BlankHeader_RejectsRequest()
    {
        var editor = new RecordingEditor();
        var service = new SortDocumentService(editor, new StubFileStore());
        var request = CreateRequest("input.docx", "output.docx") with { Header = " " };

        var error = await Assert.ThrowsAsync<OperationValidationException>(() =>
            service.ExecuteAsync(request, CancellationToken.None));

        Assert.Contains("cabecera", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(editor.SortRequest);
    }

    private static SortDocumentRequest CreateRequest(string inputPath, string outputPath)
    {
        return new SortDocumentRequest(
            inputPath,
            outputPath,
            SortingProfiles.Get(SortingProfiles.DefaultIdentifier),
            "Nombre y Apellidos");
    }

    private sealed class RecordingEditor : IDocumentSorter
    {
        internal SortDocumentRequest? SortRequest { get; private set; }

        public Task<int> SortAsync(
            SortDocumentRequest request,
            CancellationToken cancellationToken)
        {
            SortRequest = request;
            return Task.FromResult(3);
        }

    }

    private sealed class StubFileStore : IFileStore
    {
        public string GetFullPath(string path) => Path.GetFullPath(path);

        public Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public string CreateTemporaryPath(string outputPath, string label)
        {
            throw new NotSupportedException();
        }

        public Task PublishAsync(
            string temporaryPath,
            string outputPath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void DeleteIfExists(string path)
        {
            throw new NotSupportedException();
        }
    }
}
