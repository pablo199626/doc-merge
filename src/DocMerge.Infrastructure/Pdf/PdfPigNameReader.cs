using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Identity;
using DocMerge.Domain.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace DocMerge.Infrastructure.Pdf;

public sealed partial class PdfPigNameReader : IPdfNameReader
{
    private readonly PdfPageParser _parser;

    public PdfPigNameReader(PdfPageParser parser)
    {
        _parser = parser;
    }

    public async Task<PdfReadResult> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ValidatePath(path);
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(path, cancellationToken);
            var pages = ExtractPages(content, cancellationToken);
            var result = _parser.Parse(pages);
            return result with { Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)) };
        }
        catch (PdfInputException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException error)
        {
            throw new PdfInputException("El PDF está cifrado y requiere una contraseña.", error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new PdfInputException($"No se pudo leer el PDF: {error.Message}", error);
        }
    }

    private static IReadOnlyList<string> ExtractPages(
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(content);
        var pages = new List<string>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extracted = ContentOrderTextExtractor.GetText(page);
            pages.Add(CanonicalizePage(extracted));
        }

        return pages;
    }

    private static string CanonicalizePage(string extracted)
    {
        var output = new List<string>();
        foreach (var line in extracted.Split('\n'))
        {
            var normalized = IdentityNormalizer.NormalizeWhitespace(line);
            AppendCanonicalLine(normalized, output);
        }

        return string.Join('\n', output);
    }

    private static void AppendCanonicalLine(string line, ICollection<string> output)
    {
        if (CombinedHeaderRegex().IsMatch(line))
        {
            output.Add("Nº");
            output.Add("Nombre y apellidos");
            return;
        }

        var entry = CombinedEntryRegex().Match(line);
        if (entry.Success)
        {
            output.Add(entry.Groups[1].Value);
            output.Add(entry.Groups[2].Value);
            return;
        }

        output.Add(line);
    }

    private static void ValidatePath(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfInputException("El listado debe tener extensión .pdf.");
        }

        if (!File.Exists(path))
        {
            throw new PdfInputException($"No existe el archivo PDF: {path}");
        }
    }

    [GeneratedRegex(@"^N[.º°oª]*\s+Nombre\s+y\s+apellidos$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CombinedHeaderRegex();

    [GeneratedRegex(@"^(\d+)\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CombinedEntryRegex();
}
