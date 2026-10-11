using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Identity;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxTemplateReader : IGenerationTemplateReader
{
    private const int SupportedColumnCount = 3;

    public async Task<GenerationTemplateSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken)
    {
        ValidatePath(path);
        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            return Parse(path, content, header);
        }
        catch (DocumentStructureException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or XmlException)
        {
            throw new DocumentStructureException(
                $"No se pudo leer la plantilla DOCX: {error.Message}", error);
        }
    }

    internal static GenerationTemplateSnapshot Parse(
        string path,
        byte[] content,
        string header)
    {
        var package = DocxPackage.FromBytes(content);
        if (!package.Parts.TryGetValue(DocxNames.DocumentPart, out var documentPart))
        {
            throw new DocumentStructureException("La plantilla no contiene word/document.xml.");
        }

        var document = DocxXml.Parse(documentPart);
        var location = DocxXml.LocateStrictTable(document, header);
        var rows = location.Table.Elements(DocxNames.Word + "tr").ToArray();
        var headerCells = DocxXml.RowCells(rows[location.HeaderIndex]);
        ValidateColumnCount(headerCells.Count, "La cabecera de la plantilla");
        var dataRows = rows.Skip(location.HeaderIndex + 1).ToArray();
        if (dataRows.Length != 1 || !IsEmptyRow(dataRows[0]))
        {
            throw new DocumentStructureException(
                "La plantilla debe contener exclusivamente una fila vacía de prototipo después de la cabecera.");
        }

        var prototype = dataRows[0];
        ValidateColumnCount(DocxXml.RowCells(prototype).Count, "La fila de prototipo");
        var headers = headerCells.Select(cell =>
            IdentityNormalizer.NormalizeWhitespace(DocxXml.ElementText(cell))).ToArray();
        return new GenerationTemplateSnapshot(
            path,
            Convert.ToHexStringLower(SHA256.HashData(content)),
            location.TableIndex,
            location.HeaderIndex,
            location.NameColumnIndex,
            headers,
            prototype.ToString(SaveOptions.DisableFormatting));
    }

    private static bool IsEmptyRow(XElement row)
    {
        return DocxXml.RowCells(row).All(cell =>
            IdentityNormalizer.NormalizeWhitespace(DocxXml.ElementText(cell)).Length == 0);
    }

    private static void ValidateColumnCount(int actual, string description)
    {
        if (actual != SupportedColumnCount)
        {
            throw new DocumentStructureException(
                $"{description} tiene {actual} celdas; se esperaban {SupportedColumnCount}.");
        }
    }

    private static void ValidatePath(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentStructureException("La plantilla debe tener extensión .docx.");
        }

        if (!File.Exists(path))
        {
            throw new DocumentStructureException($"No existe la plantilla DOCX: {path}");
        }
    }
}
