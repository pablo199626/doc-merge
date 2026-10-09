using System.IO.Compression;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Application.Validation;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxTransferValidator : ITransferResultValidator
{
    private static readonly HashSet<string> AllowedChangedParts =
    [
        DocxNames.DocumentPart,
        DocxNames.StylesPart,
        DocxNames.NumberingPart,
        DocxNames.RelationshipsPart,
        DocxNames.ContentTypesPart,
    ];

    private readonly IDocumentReader _reader;

    public DocxTransferValidator(IDocumentReader reader)
    {
        _reader = reader;
    }

    public async Task ValidateAsync(
        TransferValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reader.ReadAsync(request.ResultPath, request.Header, cancellationToken);
        ValidateRecords(result, ExpectedRecords(request.Plan), request.SortingProfile);
        ValidatePackage(request.ResultPath, request.DestinationPath, result, request.Header);
    }

    private static IReadOnlyList<DocumentRecord> ExpectedRecords(TransferPlan plan)
    {
        return plan.Actions.Select(ExpectedRecord)
            .Where(record => record is not null)
            .Select(record => record!)
            .ToArray();
    }

    private static DocumentRecord? ExpectedRecord(TransferAction action)
    {
        if (action.ActionType is ActionType.Add or ActionType.Replace)
        {
            return action.Source;
        }

        return action.ActionType == ActionType.Keep ? action.Destination : null;
    }

    private static void ValidateRecords(
        DocumentSnapshot result,
        IReadOnlyList<DocumentRecord> expected,
        SortingProfile profile)
    {
        var ordered = expected.OrderBy(
            record => profile.CreateKey(record.OriginalName), SortKeyComparer.Instance).ToArray();
        if (result.Records.Count != ordered.Length)
        {
            throw new TransferValidationException(
                $"El resultado contiene {result.Records.Count} personas; se esperaban {ordered.Length}.");
        }

        for (var index = 0; index < ordered.Length; index++)
        {
            ValidateRecord(result.Records[index], ordered[index]);
        }
    }

    private static void ValidateRecord(DocumentRecord actual, DocumentRecord expected)
    {
        if (!string.Equals(actual.IdentityKey, expected.IdentityKey, StringComparison.Ordinal))
        {
            throw new TransferValidationException(
                $"Orden o identidad incorrectos: {actual.OriginalName} no corresponde a {expected.OriginalName}.");
        }

        if (!XNode.DeepEquals(RowSignature(actual.RowXml), RowSignature(expected.RowXml)))
        {
            throw new TransferValidationException(
                $"La información o formato de {actual.OriginalName} no coincide con su procedencia.");
        }
    }

    private static XElement RowSignature(string rowXml)
    {
        var row = XElement.Parse(rowXml, LoadOptions.PreserveWhitespace);
        foreach (var element in row.DescendantsAndSelf())
        {
            NormalizeReferences(element);
        }

        return row;
    }

    private static void NormalizeReferences(XElement element)
    {
        if ((DocxNames.StyleTags.Contains(element.Name)
            || element.Name == DocxNames.Word + "numId")
            && element.Attribute(DocxNames.Value) is { } value)
        {
            value.Value = "REFERENCIA_REMAPEADA";
        }

        foreach (var attribute in element.Attributes()
            .Where(attribute => DocxNames.RelationshipAttributes.Contains(attribute.Name)))
        {
            attribute.Value = "REFERENCIA_REMAPEADA";
        }
    }

    private static void ValidatePackage(
        string resultPath,
        string destinationPath,
        DocumentSnapshot result,
        string header)
    {
        var resultParts = ReadParts(resultPath);
        var destinationParts = ReadParts(destinationPath);
        ValidateDestinationParts(destinationParts, resultParts);
        ValidateOutsideTable(destinationParts, resultParts, header);
        ValidateReferences(resultParts, result);
    }

    private static Dictionary<string, byte[]> ReadParts(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.Entries.ToDictionary(
                entry => entry.FullName,
                ReadEntry,
                StringComparer.Ordinal);
        }
        catch (InvalidDataException error)
        {
            throw new TransferValidationException(
                "El paquete generado contiene datos corruptos.", error);
        }
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void ValidateDestinationParts(
        IReadOnlyDictionary<string, byte[]> destination,
        IReadOnlyDictionary<string, byte[]> result)
    {
        foreach (var part in destination)
        {
            if (!result.TryGetValue(part.Key, out var content))
            {
                throw new TransferValidationException(
                    $"El resultado perdió una parte del destino: {part.Key}.");
            }

            if (!AllowedChangedParts.Contains(part.Key) && !part.Value.SequenceEqual(content))
            {
                throw new TransferValidationException(
                    $"Se modificó una parte no autorizada del destino: {part.Key}.");
            }
        }
    }

    private static void ValidateOutsideTable(
        IReadOnlyDictionary<string, byte[]> destination,
        IReadOnlyDictionary<string, byte[]> result,
        string header)
    {
        var destinationDocument = DocxXml.Parse(destination[DocxNames.DocumentPart]);
        var resultDocument = DocxXml.Parse(result[DocxNames.DocumentPart]);
        DocxXml.LocateStrictTable(destinationDocument, header).Table.Remove();
        DocxXml.LocateStrictTable(resultDocument, header).Table.Remove();
        if (!XNode.DeepEquals(destinationDocument, resultDocument))
        {
            throw new TransferValidationException(
                "Se modificó contenido del destino situado fuera de la tabla objetivo.");
        }
    }

    private static void ValidateReferences(
        IReadOnlyDictionary<string, byte[]> parts,
        DocumentSnapshot result)
    {
        var styles = ReadDeclaredIds(parts, DocxNames.StylesPart, "style", DocxNames.StyleId);
        var numbering = ReadDeclaredIds(
            parts, DocxNames.NumberingPart, "num", DocxNames.Word + "numId");
        foreach (var record in result.Records)
        {
            ValidateRecordReferences(record, styles, numbering);
        }
    }

    private static HashSet<string> ReadDeclaredIds(
        IReadOnlyDictionary<string, byte[]> parts,
        string part,
        string element,
        XName attribute)
    {
        if (!parts.TryGetValue(part, out var content))
        {
            return [];
        }

        return DocxXml.Parse(content).Root!.Elements(DocxNames.Word + element)
            .Select(item => item.Attribute(attribute)?.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateRecordReferences(
        DocumentRecord record,
        IReadOnlySet<string> styles,
        IReadOnlySet<string> numbering)
    {
        var missingStyles = record.Dependencies.Styles.Where(style => !styles.Contains(style)).ToArray();
        if (missingStyles.Length > 0)
        {
            throw new TransferValidationException(
                $"La fila {record.Location} usa estilos inexistentes: [{string.Join(", ", missingStyles)}]");
        }

        var missingNumbering = record.Dependencies.Numbering
            .Where(number => number != "0" && !numbering.Contains(number)).ToArray();
        if (missingNumbering.Length > 0)
        {
            throw new TransferValidationException(
                $"La fila {record.Location} usa numeraciones inexistentes: [{string.Join(", ", missingNumbering)}]");
        }
    }
}
