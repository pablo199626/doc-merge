using System.Xml.Linq;

namespace DocMerge.Infrastructure.Docx;

internal static class DocxRowImporter
{
    internal static IReadOnlyList<XElement> Import(
        DocxPackage source,
        DocxPackage destination,
        IReadOnlyList<XElement> rows,
        CancellationToken cancellationToken)
    {
        var styles = StyleImporter.Import(source, destination, rows, cancellationToken);
        var numbering = NumberingImporter.Import(
            source, destination, rows, styles, cancellationToken);
        if (styles.Document is not null)
        {
            destination.Parts[DocxNames.StylesPart] = DocxXml.Serialize(styles.Document);
        }

        if (numbering.Document is not null)
        {
            destination.Parts[DocxNames.NumberingPart] = DocxXml.Serialize(numbering.Document);
        }

        var relationships = RelationshipImporter.Import(
            source, destination, rows, cancellationToken);
        destination.Parts[DocxNames.RelationshipsPart] = DocxXml.Serialize(relationships.Document);
        var mappings = new DependencyMappings(
            styles.Mapping, numbering.Mapping, relationships.Mapping);
        return rows.Select(row => RemapRow(row, mappings)).ToArray();
    }

    private static XElement RemapRow(XElement source, DependencyMappings mappings)
    {
        var row = new XElement(source);
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

    private sealed record DependencyMappings(
        IReadOnlyDictionary<string, string> Styles,
        IReadOnlyDictionary<string, string> Numbering,
        IReadOnlyDictionary<string, string> Relationships);
}
