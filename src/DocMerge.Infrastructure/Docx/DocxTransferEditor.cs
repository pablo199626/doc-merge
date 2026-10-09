using System.Security.Cryptography;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Transfer;

namespace DocMerge.Infrastructure.Docx;

internal sealed class DocxTransferEditor
{
    internal async Task<int> ApplyAsync(
        ApplyTransferPlanRequest request,
        CancellationToken cancellationToken)
    {
        var sourceBytes = await File.ReadAllBytesAsync(request.SourcePath, cancellationToken);
        var destinationBytes = await File.ReadAllBytesAsync(request.DestinationPath, cancellationToken);
        VerifyHash(sourceBytes, request.Plan.SourceHash, "El documento de origen cambió después de crear el plan.");
        VerifyHash(destinationBytes, request.Plan.DestinationHash, "El documento de destino cambió después de crear el plan.");
        var source = DocxPackage.FromBytes(sourceBytes);
        var destination = DocxPackage.FromBytes(destinationBytes);
        var rows = SourceRows(request.Plan);
        var mappings = ImportDependencies(source, destination, rows);
        var count = ApplyActions(destination, request.Plan, mappings, request.Header);
        await destination.WriteAsync(request.OutputPath, cancellationToken);
        return count;
    }

    private static IReadOnlyList<XElement> SourceRows(TransferPlan plan)
    {
        return plan.Actions
            .Where(action => action.ActionType is ActionType.Add or ActionType.Replace)
            .Select(action => XElement.Parse(action.Source!.RowXml, LoadOptions.PreserveWhitespace))
            .ToArray();
    }

    private static DependencyMappings ImportDependencies(
        DocxPackage source,
        DocxPackage destination,
        IReadOnlyList<XElement> rows)
    {
        var styles = StyleImporter.Import(source, destination, rows);
        var numbering = NumberingImporter.Import(source, destination, rows, styles);
        if (styles.Document is not null)
        {
            destination.Parts[DocxNames.StylesPart] = DocxXml.Serialize(styles.Document);
        }

        if (numbering.Document is not null)
        {
            destination.Parts[DocxNames.NumberingPart] = DocxXml.Serialize(numbering.Document);
        }

        var relationships = RelationshipImporter.Import(source, destination, rows);
        destination.Parts[DocxNames.RelationshipsPart] = DocxXml.Serialize(relationships.Document);
        return new DependencyMappings(styles.Mapping, numbering.Mapping, relationships.Mapping);
    }

    private static int ApplyActions(
        DocxPackage destination,
        TransferPlan plan,
        DependencyMappings mappings,
        string header)
    {
        var document = DocxXml.Parse(destination.Parts[DocxNames.DocumentPart]);
        var location = DocxXml.LocateStrictTable(document, header);
        var rows = location.Table.Elements(DocxNames.Word + "tr")
            .Select((row, index) => (index, row))
            .ToDictionary(item => item.index, item => item.row);
        var additions = new List<XElement>();
        foreach (var action in plan.Actions)
        {
            ApplyAction(location.Table, rows, additions, action, mappings);
        }

        location.Table.Add(additions);
        destination.Parts[DocxNames.DocumentPart] = DocxXml.Serialize(document);
        return additions.Count;
    }

    private static void ApplyAction(
        XElement table,
        IReadOnlyDictionary<int, XElement> rows,
        ICollection<XElement> additions,
        TransferAction action,
        DependencyMappings mappings)
    {
        if (action.ActionType == ActionType.Delete && action.Destination is not null)
        {
            rows[action.Destination.RowIndex].Remove();
            return;
        }

        if (action.ActionType == ActionType.Replace && action.Destination is not null)
        {
            rows[action.Destination.RowIndex].ReplaceWith(RemapSourceRow(action, mappings));
            return;
        }

        if (action.ActionType == ActionType.Add)
        {
            additions.Add(RemapSourceRow(action, mappings));
        }
    }

    private static XElement RemapSourceRow(
        TransferAction action,
        DependencyMappings mappings)
    {
        var row = XElement.Parse(action.Source!.RowXml, LoadOptions.PreserveWhitespace);
        foreach (var element in row.DescendantsAndSelf())
        {
            RemapValue(element, mappings);
            RemapRelationships(element, mappings.Relationships);
        }

        return row;
    }

    private static void RemapValue(XElement element, DependencyMappings mappings)
    {
        var attribute = element.Attribute(DocxNames.Value);
        if (attribute is null)
        {
            return;
        }

        if (DocxNames.StyleTags.Contains(element.Name)
            && mappings.Styles.TryGetValue(attribute.Value, out var style))
        {
            attribute.Value = style;
        }

        if (element.Name == DocxNames.Word + "numId"
            && mappings.Numbering.TryGetValue(attribute.Value, out var number))
        {
            attribute.Value = number;
        }
    }

    private static void RemapRelationships(
        XElement element,
        IReadOnlyDictionary<string, string> mapping)
    {
        foreach (var attribute in element.Attributes()
            .Where(attribute => DocxNames.RelationshipAttributes.Contains(attribute.Name)))
        {
            if (mapping.TryGetValue(attribute.Value, out var value))
            {
                attribute.Value = value;
            }
        }
    }

    private static void VerifyHash(byte[] content, string expected, string message)
    {
        if (expected.Length == 0)
        {
            return;
        }

        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new DocumentStructureException(message);
        }
    }

    private sealed record DependencyMappings(
        IReadOnlyDictionary<string, string> Styles,
        IReadOnlyDictionary<string, string> Numbering,
        IReadOnlyDictionary<string, string> Relationships);
}
