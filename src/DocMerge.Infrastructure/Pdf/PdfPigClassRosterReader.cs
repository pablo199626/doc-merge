using System.Security.Cryptography;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Generation;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace DocMerge.Infrastructure.Pdf;

public sealed class PdfPigClassRosterReader : IClassRosterReader
{
    private readonly GroupRosterPageParser _parser;

    public PdfPigClassRosterReader(GroupRosterPageParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<ClassRoster> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ValidatePath(path);
        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            var pages = ExtractPages(content, cancellationToken);
            var parsed = _parser.Parse(pages, Path.GetFileName(path));
            return new ClassRoster(
                path,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                parsed.ClassIdentifier,
                parsed.Entries,
                pages.Count,
                hasDeclaredTotal: false);
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
        catch (Exception error) when (error is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or PdfDocumentFormatException)
        {
            throw new PdfInputException($"No se pudo leer el PDF de clase: {error.Message}", error);
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
            pages.Add(ContentOrderTextExtractor.GetText(page));
        }

        return pages;
    }

    private static void ValidatePath(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfInputException("El listado de clase debe tener extensión .pdf.");
        }

        if (!File.Exists(path))
        {
            throw new PdfInputException($"No existe el PDF de clase: {path}");
        }
    }
}
