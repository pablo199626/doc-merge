using DocMerge.Application.Pdf;
using DocMerge.Infrastructure.Pdf;

namespace DocMerge.Infrastructure.Tests.Pdf;

public sealed class PdfPigNameReaderTests
{
    [Fact]
    public async Task ReadAsync_MalformedPdf_ThrowsControlledInputException()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("malformed.pdf");
        await File.WriteAllTextAsync(path, "%PDF-1.7 invalid content");
        var reader = new PdfPigNameReader(new PdfPageParser());

        var error = await Assert.ThrowsAsync<PdfInputException>(() =>
            reader.ReadAsync(path, CancellationToken.None));

        Assert.NotNull(error.InnerException);
    }
}
