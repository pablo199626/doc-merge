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
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task ValidateAsync(
        TransferValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reader.ReadAsync(request.ResultPath, request.Header, cancellationToken);
        var expected = ExpectedRecords(request.Plan)
            .OrderBy(
                record => request.SortingProfile.CreateKey(record.OriginalName),
                SortKeyComparer.Instance)
            .ToArray();
        ValidateRecords(result, expected, cancellationToken);
        var context = new PackageValidationContext(request, result, expected);
        await ValidatePackageAsync(context, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        if (result.Records.Count != expected.Count)
        {
            throw new TransferValidationException(
                $"El resultado contiene {result.Records.Count} personas; se esperaban {expected.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRecord(result.Records[index], expected[index]);
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

    private static async Task ValidatePackageAsync(
        PackageValidationContext context,
        CancellationToken cancellationToken)
    {
        var request = context.Request;
        var resultParts = await ReadPartsAsync(request.ResultPath, cancellationToken);
        var sourceParts = await ReadPartsAsync(request.SourcePath, cancellationToken);
        var destinationParts = await ReadPartsAsync(request.DestinationPath, cancellationToken);
        ValidateDestinationParts(destinationParts, resultParts, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOutsideTable(destinationParts, resultParts, request.Header);
        ValidateReferences(resultParts, context.Result, cancellationToken);
        var dependencyContext = new DocxDependencyValidator.DependencyValidationContext(
            new DocxDependencyValidator.DocumentPackage(request.SourcePath, sourceParts),
            new DocxDependencyValidator.DocumentPackage(request.DestinationPath, destinationParts),
            resultParts);
        DocxDependencyValidator.Validate(
            dependencyContext,
            context.Expected,
            context.Result.Records,
            cancellationToken);
    }

    private static async Task<Dictionary<string, byte[]>> ReadPartsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                parts.Add(entry.FullName, await ReadEntryAsync(entry, cancellationToken));
            }

            return parts;
        }
        catch (InvalidDataException error)
        {
            throw new TransferValidationException(
                "El paquete generado contiene datos corruptos.", error);
        }
    }

    private static async Task<byte[]> ReadEntryAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static void ValidateDestinationParts(
        IReadOnlyDictionary<string, byte[]> destination,
        IReadOnlyDictionary<string, byte[]> result,
        CancellationToken cancellationToken)
    {
        foreach (var part in destination)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        DocumentSnapshot result,
        CancellationToken cancellationToken)
    {
        var styles = ReadDeclaredIds(parts, DocxNames.StylesPart, "style", DocxNames.StyleId);
        var numbering = ReadDeclaredIds(
            parts, DocxNames.NumberingPart, "num", DocxNames.Word + "numId");
        foreach (var record in result.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private sealed record PackageValidationContext(
        TransferValidationRequest Request,
        DocumentSnapshot Result,
        IReadOnlyList<DocumentRecord> Expected);
}
