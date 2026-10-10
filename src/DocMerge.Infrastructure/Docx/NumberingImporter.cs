using System.Globalization;
using System.Xml.Linq;
using DocMerge.Application.Documents;

namespace DocMerge.Infrastructure.Docx;

internal sealed class NumberingImporter
{
    private readonly StyleImportResult _styles;
    private readonly Dictionary<string, string> _mapping = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _abstractMapping = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, XElement> _sourceNumbers;
    private readonly IReadOnlyDictionary<string, XElement> _sourceAbstracts;
    private readonly XDocument _destinationDocument;
    private int _nextNumberId;
    private int _nextAbstractId;

    private NumberingImporter(
        StyleImportResult styles,
        NumberingDefinitions definitions)
    {
        _styles = styles;
        _sourceNumbers = definitions.SourceNumbers;
        _sourceAbstracts = definitions.SourceAbstracts;
        _destinationDocument = definitions.DestinationDocument;
        _nextNumberId = NextNumericId(definitions.DestinationNumbers.Keys);
        _nextAbstractId = NextNumericId(definitions.DestinationAbstracts.Keys);
    }

    internal static NumberingImportResult Import(
        DocxPackage source,
        DocxPackage destination,
        IReadOnlyList<XElement> rows,
        StyleImportResult styles,
        CancellationToken cancellationToken)
    {
        var references = CollectReferences(source, rows, styles, cancellationToken);
        if (references.Count == 0)
        {
            return NumberingImportResult.Empty;
        }

        var definitions = LoadDefinitions(source, destination);
        var importer = new NumberingImporter(styles, definitions);
        foreach (var id in references.OrderBy(id => id, NumberingSortKey.Instance))
        {
            cancellationToken.ThrowIfCancellationRequested();
            importer.ImportNumber(id);
        }

        importer.RemapStyleNumbering();
        return new NumberingImportResult(importer._mapping, importer._destinationDocument);
    }

    private static HashSet<string> CollectReferences(
        DocxPackage source,
        IReadOnlyList<XElement> rows,
        StyleImportResult styles,
        CancellationToken cancellationToken)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = row.DescendantsAndSelf(DocxNames.Word + "numId")
                .Select(element => element.Attribute(DocxNames.Value)?.Value)
                .Where(value => value is not null and not "0");
            foreach (var value in values)
            {
                references.Add(value!);
            }
        }

        AddStyleReferences(source, styles, references, cancellationToken);
        return references;
    }

    private static void AddStyleReferences(
        DocxPackage source,
        StyleImportResult styles,
        ISet<string> references,
        CancellationToken cancellationToken)
    {
        if (styles.ImportedIds.Count == 0
            || !source.Parts.TryGetValue(DocxNames.StylesPart, out var content))
        {
            return;
        }

        var sourceStyles = DocxXml.Parse(content).Root!
            .Elements(DocxNames.Word + "style");
        foreach (var style in sourceStyles.Where(style => IsImportedStyle(style, styles)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var id in style.Descendants(DocxNames.Word + "numId")
                .Select(element => element.Attribute(DocxNames.Value)?.Value)
                .Where(value => value is not null and not "0"))
            {
                references.Add(id!);
            }
        }
    }

    private static bool IsImportedStyle(XElement style, StyleImportResult styles)
    {
        var original = style.Attribute(DocxNames.StyleId)?.Value;
        return original is not null
            && styles.Mapping.TryGetValue(original, out var mapped)
            && styles.ImportedIds.Contains(mapped);
    }

    private static NumberingDefinitions LoadDefinitions(
        DocxPackage source,
        DocxPackage destination)
    {
        if (!source.Parts.TryGetValue(DocxNames.NumberingPart, out var sourcePart)
            || !destination.Parts.TryGetValue(DocxNames.NumberingPart, out var destinationPart))
        {
            throw new DocumentStructureException(
                "No se puede conservar la numeración: falta word/numbering.xml.");
        }

        var sourceRoot = DocxXml.Parse(sourcePart).Root!;
        var destinationDocument = DocxXml.Parse(destinationPart);
        var sourceNumbers = MapByAttribute(sourceRoot, "num", "numId");
        var sourceAbstracts = MapByAttribute(sourceRoot, "abstractNum", "abstractNumId");
        var destinationNumbers = MapByAttribute(destinationDocument.Root!, "num", "numId");
        var destinationAbstracts = MapByAttribute(
            destinationDocument.Root!, "abstractNum", "abstractNumId");
        return new NumberingDefinitions(
            sourceNumbers,
            sourceAbstracts,
            destinationNumbers,
            destinationAbstracts,
            destinationDocument);
    }

    private static Dictionary<string, XElement> MapByAttribute(
        XElement root,
        string elementName,
        string attributeName)
    {
        var attribute = DocxNames.Word + attributeName;
        return root.Elements(DocxNames.Word + elementName)
            .Where(element => element.Attribute(attribute) is not null)
            .ToDictionary(element => element.Attribute(attribute)!.Value, StringComparer.Ordinal);
    }

    private void ImportNumber(string id)
    {
        if (!_sourceNumbers.TryGetValue(id, out var sourceNumber))
        {
            throw new DocumentStructureException(
                $"El origen referencia una numeración inexistente: {id}.");
        }

        var abstractId = GetAbstractId(id, sourceNumber);
        ImportAbstract(abstractId);
        var newId = (_nextNumberId++).ToString(CultureInfo.InvariantCulture);
        _mapping.Add(id, newId);
        var clone = new XElement(sourceNumber);
        clone.SetAttributeValue(DocxNames.Word + "numId", newId);
        clone.Element(DocxNames.Word + "abstractNumId")!
            .SetAttributeValue(DocxNames.Value, _abstractMapping[abstractId]);
        _destinationDocument.Root!.Add(clone);
    }

    private string GetAbstractId(string numberId, XElement number)
    {
        var id = number.Element(DocxNames.Word + "abstractNumId")
            ?.Attribute(DocxNames.Value)?.Value;
        if (id is null || !_sourceAbstracts.ContainsKey(id))
        {
            throw new DocumentStructureException(
                $"La numeración {numberId} no tiene una definición abstracta válida.");
        }

        return id;
    }

    private void ImportAbstract(string id)
    {
        if (_abstractMapping.ContainsKey(id))
        {
            return;
        }

        var newId = (_nextAbstractId++).ToString(CultureInfo.InvariantCulture);
        _abstractMapping.Add(id, newId);
        var clone = new XElement(_sourceAbstracts[id]);
        clone.SetAttributeValue(DocxNames.Word + "abstractNumId", newId);
        RemapAbstractStyles(clone);
        var firstNumber = _destinationDocument.Root!.Elements(DocxNames.Word + "num").FirstOrDefault();
        if (firstNumber is null)
        {
            _destinationDocument.Root!.Add(clone);
            return;
        }

        firstNumber.AddBeforeSelf(clone);
    }

    private void RemapAbstractStyles(XElement definition)
    {
        var references = definition.Descendants()
            .Where(element => element.Name == DocxNames.Word + "styleLink"
                || element.Name == DocxNames.Word + "numStyleLink");
        foreach (var reference in references)
        {
            var attribute = reference.Attribute(DocxNames.Value);
            if (attribute is not null && _styles.Mapping.TryGetValue(attribute.Value, out var mapped))
            {
                attribute.Value = mapped;
            }
        }
    }

    private void RemapStyleNumbering()
    {
        if (_styles.Document?.Root is null)
        {
            return;
        }

        var importedStyles = _styles.Document.Root.Elements(DocxNames.Word + "style")
            .Where(style => _styles.ImportedIds.Contains(
                style.Attribute(DocxNames.StyleId)?.Value ?? string.Empty));
        foreach (var number in importedStyles.SelectMany(
            style => style.Descendants(DocxNames.Word + "numId")))
        {
            var attribute = number.Attribute(DocxNames.Value);
            if (attribute is not null && _mapping.TryGetValue(attribute.Value, out var mapped))
            {
                attribute.Value = mapped;
            }
        }
    }

    private static int NextNumericId(IEnumerable<string> values)
    {
        return values.Select(value => int.TryParse(value, out var number) ? number : 0)
            .DefaultIfEmpty(0).Max() + 1;
    }

    private sealed class NumberingSortKey : IComparer<string>
    {
        internal static readonly NumberingSortKey Instance = new();

        public int Compare(string? left, string? right)
        {
            if (int.TryParse(left, out var leftNumber) && int.TryParse(right, out var rightNumber))
            {
                return leftNumber.CompareTo(rightNumber);
            }

            if (int.TryParse(left, out _))
            {
                return -1;
            }

            if (int.TryParse(right, out _))
            {
                return 1;
            }

            return StringComparer.Ordinal.Compare(left, right);
        }
    }

    private sealed record NumberingDefinitions(
        IReadOnlyDictionary<string, XElement> SourceNumbers,
        IReadOnlyDictionary<string, XElement> SourceAbstracts,
        IReadOnlyDictionary<string, XElement> DestinationNumbers,
        IReadOnlyDictionary<string, XElement> DestinationAbstracts,
        XDocument DestinationDocument);
}

internal sealed record NumberingImportResult(
    IReadOnlyDictionary<string, string> Mapping,
    XDocument? Document)
{
    internal static readonly NumberingImportResult Empty = new(
        new Dictionary<string, string>(), null);
}
