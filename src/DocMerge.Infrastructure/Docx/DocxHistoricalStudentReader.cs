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
            var richContentReader = new DocxHistoricalRichContentReader(package);
            var candidates = document.Records.Select(record => new HistoricalStudentCandidate(
                record,
                StudentNameMatchKey.Create(record.OriginalName),
                CreateReviewFields(
                    record,
                    document.NameColumnIndex,
                    headers,
                    richContentReader))).ToArray();
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

    private static IReadOnlyList<HistoricalInformationField> CreateReviewFields(
        Domain.Documents.DocumentRecord record,
        int nameColumnIndex,
        IReadOnlyList<string> headers,
        DocxHistoricalRichContentReader richContentReader)
    {
        var row = XElement.Parse(record.RowXml, LoadOptions.PreserveWhitespace);
        return DocxXml.RowCells(row)
            .Select((cell, index) => (cell, index))
            .Where(item => item.index != nameColumnIndex)
            .Select(item => new HistoricalInformationField(
                headers[item.index],
                DocxXml.ElementText(item.cell),
                HasNonTextContent(item.cell),
                richContentReader.Read(item.cell)))
            .ToArray();
    }

    private static bool HasNonTextContent(XElement cell)
    {
        return cell.Descendants().Any(element =>
            element.Name.LocalName is "drawing" or "object" or "pict");
    }

}
