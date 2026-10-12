using DocMerge.Application.Generation;
using DocMerge.Infrastructure.Files;

namespace DocMerge.Infrastructure.Tests.Files;

public sealed class GenerationInputDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_TemporaryBatchInsideHistoricalTree_ExcludesGeneratedDocuments()
    {
        using var temporary = new TemporaryDirectory();
        var historical = temporary.File("historical");
        var rosters = temporary.File("rosters");
        Directory.CreateDirectory(historical);
        Directory.CreateDirectory(rosters);
        File.WriteAllText(Path.Combine(historical, "student.docx"), "historical");
        File.WriteAllText(Path.Combine(rosters, "2A.pdf"), "roster");
        var working = Path.Combine(historical, ".docmerge_123_output");
        Directory.CreateDirectory(working);
        File.WriteAllText(Path.Combine(working, "2A.docx"), "generated");
        var template = temporary.File("template.docx");
        File.WriteAllText(template, "template");

        var result = await new GenerationInputDiscovery().DiscoverAsync(
            historical, rosters, template, CancellationToken.None);

        Assert.Single(result.HistoricalDocumentPaths);
        Assert.EndsWith("student.docx", result.HistoricalDocumentPaths[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_CancelledOperation_StopsBeforeEnumeration()
    {
        using var temporary = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GenerationInputDiscovery().DiscoverAsync(
                temporary.RootPath,
                temporary.RootPath,
                temporary.File("template.docx"),
                cancellation.Token));
    }

    [Fact]
    public async Task DiscoverAsync_HistoricalFolderWithoutDocx_IdentifiesHistoricalInput()
    {
        using var temporary = new TemporaryDirectory();
        var rosters = temporary.File("rosters");
        Directory.CreateDirectory(rosters);
        File.WriteAllText(Path.Combine(rosters, "2A.pdf"), "roster");

        var error = await Assert.ThrowsAsync<GenerationInputException>(() =>
            new GenerationInputDiscovery().DiscoverAsync(
                temporary.RootPath,
                rosters,
                temporary.File("template.docx"),
                CancellationToken.None));

        Assert.Equal(GenerationInputType.HistoricalDocuments, error.InputType);
    }
}
