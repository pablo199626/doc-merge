using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Identity;

namespace DocMerge.Infrastructure.Docx;

internal static class DocxXml
{
    internal static XDocument Parse(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    internal static byte[] Serialize(XDocument document)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = document.Declaration is null,
        };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    internal static string ElementText(XElement element)
    {
        return string.Concat(element.Descendants(DocxNames.Word + "t").Select(node => node.Value));
    }

    internal static IReadOnlyList<XElement> RowCells(XElement row)
    {
        return row.Elements(DocxNames.Word + "tc").ToArray();
    }

    internal static TableLocation LocateStrictTable(XDocument document, string header)
    {
        var headerKey = IdentityNormalizer.Normalize(header);
        var matches = FindHeaderMatches(document, headerKey).ToArray();
        if (matches.Length == 0)
        {
            throw new DocumentStructureException(
                $"No se encontró una tabla con la cabecera exacta \"{header}\".");
        }

        if (matches.Length > 1)
        {
            var locations = string.Join(", ", matches.Select(match =>
                $"tabla {match.TableIndex}, fila {match.HeaderIndex}"));
            throw new DocumentStructureException($"La tabla de personas es ambigua: {locations}.");
        }

        return matches[0];
    }

    private static IEnumerable<TableLocation> FindHeaderMatches(
        XDocument document,
        string headerKey)
    {
        return document.Descendants(DocxNames.Word + "tbl")
            .SelectMany((table, tableIndex) => FindTableMatches(table, tableIndex, headerKey));
    }

    private static IEnumerable<TableLocation> FindTableMatches(
        XElement table,
        int tableIndex,
        string headerKey)
    {
        return table.Elements(DocxNames.Word + "tr")
            .SelectMany((row, rowIndex) => RowCells(row)
                .Select((cell, cellIndex) => (cell, cellIndex))
                .Where(item => IdentityNormalizer.Normalize(ElementText(item.cell)) == headerKey)
                .Select(item => new TableLocation(tableIndex, table, rowIndex, item.cellIndex)));
    }

    internal sealed record TableLocation(
        int TableIndex,
        XElement Table,
        int HeaderIndex,
        int NameColumnIndex);
}
