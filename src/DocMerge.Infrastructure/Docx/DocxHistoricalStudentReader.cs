using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Identity;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxHistoricalStudentReader : IHistoricalStudentReader
{
    public async Task<HistoricalDocumentSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            var document = DocxDocumentReader.Parse(path, content, header);
            var package = DocxPackage.FromBytes(content);
            var headers = ReadHeaders(package, document);
            var candidates = document.Records.Select(record => new HistoricalStudentCandidate(
                record,
                StudentNameMatchKey.Create(record.OriginalName),
                CreateInformationSignature(record, document.NameColumnIndex, package))).ToArray();
            return new HistoricalDocumentSnapshot(path, document.Sha256, headers, candidates);
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
                $"No se pudo preparar el DOCX histórico: {error.Message}", error);
        }
    }

    private static IReadOnlyList<string> ReadHeaders(
        DocxPackage package,
        Domain.Documents.DocumentSnapshot snapshot)
    {
        var document = DocxXml.Parse(package.Parts[DocxNames.DocumentPart]);
        var table = document.Descendants(DocxNames.Word + "tbl").ElementAt(snapshot.TableIndex);
        var row = table.Elements(DocxNames.Word + "tr").ElementAt(snapshot.HeaderIndex);
        return DocxXml.RowCells(row).Select(cell =>
            IdentityNormalizer.NormalizeWhitespace(DocxXml.ElementText(cell))).ToArray();
    }

    private static string CreateInformationSignature(
        Domain.Documents.DocumentRecord record,
        int nameColumnIndex,
        DocxPackage package)
    {
        var row = XElement.Parse(record.RowXml, LoadOptions.PreserveWhitespace);
        var cells = DocxXml.RowCells(row);
        cells[nameColumnIndex].ReplaceWith(new XElement(DocxNames.Word + "tc"));
        var signature = new StringBuilder(row.ToString(SaveOptions.DisableFormatting));
        AppendPartHash(signature, package, DocxNames.StylesPart);
        AppendPartHash(signature, package, DocxNames.NumberingPart);
        foreach (var relationship in record.Dependencies.Relationships
            .OrderBy(item => item.RelationshipId, StringComparer.Ordinal))
        {
            signature.Append('|').Append(relationship.RelationshipType)
                .Append('|').Append(relationship.IsExternal)
                .Append('|').Append(relationship.Target);
            if (!relationship.IsExternal)
            {
                AppendPartHash(signature, package, relationship.Target);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString())));
    }

    private static void AppendPartHash(
        StringBuilder signature,
        DocxPackage package,
        string partName)
    {
        if (package.Parts.TryGetValue(partName, out var content))
        {
            signature.Append('|').Append(partName).Append(':')
                .Append(Convert.ToHexStringLower(SHA256.HashData(content)));
        }
    }
}
