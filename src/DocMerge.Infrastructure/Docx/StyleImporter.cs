using System.Xml.Linq;
using DocMerge.Application.Documents;

namespace DocMerge.Infrastructure.Docx;

internal static class StyleImporter
{
    internal static StyleImportResult Import(
        DocxPackage source,
        DocxPackage destination,
        IReadOnlyList<XElement> rows)
    {
        var references = CollectReferences(rows);
        if (references.Count == 0)
        {
            return StyleImportResult.Empty;
        }

        var documents = LoadDocuments(source, destination);
        var sourceStyles = MapStyles(documents.Source.Root!);
        var destinationStyles = MapStyles(documents.Destination.Root!);
        var dependencies = CollectDependencies(references, sourceStyles);
        var conflicts = FindConflicts(dependencies, sourceStyles, destinationStyles);
        var mapping = CreateMapping(dependencies, conflicts, destinationStyles.Keys);
        var imported = ImportStyles(
            dependencies, sourceStyles, destinationStyles, documents.Destination.Root!, mapping);
        return new StyleImportResult(mapping, documents.Destination, imported);
    }

    private static HashSet<string> CollectReferences(IReadOnlyList<XElement> rows)
    {
        return rows.SelectMany(row => row.DescendantsAndSelf())
            .Where(element => DocxNames.StyleTags.Contains(element.Name))
            .Select(element => element.Attribute(DocxNames.Value)?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static StyleDocuments LoadDocuments(DocxPackage source, DocxPackage destination)
    {
        if (!source.Parts.TryGetValue(DocxNames.StylesPart, out var sourcePart)
            || !destination.Parts.TryGetValue(DocxNames.StylesPart, out var destinationPart))
        {
            throw new DocumentStructureException(
                "No se pueden conservar los estilos: falta word/styles.xml.");
        }

        return new StyleDocuments(DocxXml.Parse(sourcePart), DocxXml.Parse(destinationPart));
    }

    private static Dictionary<string, XElement> MapStyles(XElement root)
    {
        return root.Elements(DocxNames.Word + "style")
            .Where(style => style.Attribute(DocxNames.StyleId) is not null)
            .ToDictionary(
                style => style.Attribute(DocxNames.StyleId)!.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> CollectDependencies(
        IEnumerable<string> references,
        IReadOnlyDictionary<string, XElement> sourceStyles)
    {
        var pending = new Queue<string>(references);
        var result = new List<string>();
        while (pending.TryDequeue(out var id))
        {
            if (result.Contains(id, StringComparer.Ordinal))
            {
                continue;
            }

            if (!sourceStyles.TryGetValue(id, out var style))
            {
                throw new DocumentStructureException(
                    $"El origen referencia un estilo inexistente: {id}.");
            }

            result.Add(id);
            foreach (var linked in LinkedStyles(style))
            {
                pending.Enqueue(linked);
            }
        }

        return result;
    }

    private static IEnumerable<string> LinkedStyles(XElement style)
    {
        return new[] { "basedOn", "link" }
            .Select(name => style.Element(DocxNames.Word + name)?.Attribute(DocxNames.Value)?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!);
    }

    private static HashSet<string> FindConflicts(
        IReadOnlyList<string> dependencies,
        IReadOnlyDictionary<string, XElement> source,
        IReadOnlyDictionary<string, XElement> destination)
    {
        var conflicts = dependencies
            .Where(id => destination.TryGetValue(id, out var existing)
                && !XNode.DeepEquals(source[id], existing))
            .ToHashSet(StringComparer.Ordinal);
        while (AddInheritedConflicts(dependencies, source, destination, conflicts))
        {
        }

        return conflicts;
    }

    private static bool AddInheritedConflicts(
        IReadOnlyList<string> dependencies,
        IReadOnlyDictionary<string, XElement> source,
        IReadOnlyDictionary<string, XElement> destination,
        ISet<string> conflicts)
    {
        var added = false;
        foreach (var id in dependencies)
        {
            if (conflicts.Contains(id) || !destination.ContainsKey(id))
            {
                continue;
            }

            if (LinkedStyles(source[id]).Any(conflicts.Contains))
            {
                conflicts.Add(id);
                added = true;
            }
        }

        return added;
    }

    private static Dictionary<string, string> CreateMapping(
        IReadOnlyList<string> dependencies,
        IReadOnlySet<string> conflicts,
        IEnumerable<string> destinationIds)
    {
        var used = destinationIds.ToHashSet(StringComparer.Ordinal);
        return dependencies.ToDictionary(
            id => id,
            id => conflicts.Contains(id) ? CreateUniqueId(id, used) : id,
            StringComparer.Ordinal);
    }

    private static string CreateUniqueId(string id, ISet<string> used)
    {
        var candidate = $"DO_{id}";
        var number = 2;
        while (!used.Add(candidate))
        {
            candidate = $"DO_{id}_{number}";
            number++;
        }

        return candidate;
    }

    private static HashSet<string> ImportStyles(
        IReadOnlyList<string> dependencies,
        IReadOnlyDictionary<string, XElement> source,
        IReadOnlyDictionary<string, XElement> destination,
        XElement destinationRoot,
        IReadOnlyDictionary<string, string> mapping)
    {
        var imported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in dependencies)
        {
            if (destination.ContainsKey(mapping[id]))
            {
                continue;
            }

            var clone = new XElement(source[id]);
            clone.SetAttributeValue(DocxNames.StyleId, mapping[id]);
            RemapLinkedStyles(clone, mapping);
            destinationRoot.Add(clone);
            imported.Add(mapping[id]);
        }

        return imported;
    }

    private static void RemapLinkedStyles(
        XElement style,
        IReadOnlyDictionary<string, string> mapping)
    {
        foreach (var name in new[] { "basedOn", "link" })
        {
            var attribute = style.Element(DocxNames.Word + name)?.Attribute(DocxNames.Value);
            if (attribute is not null && mapping.TryGetValue(attribute.Value, out var mapped))
            {
                attribute.Value = mapped;
            }
        }
    }

    private sealed record StyleDocuments(XDocument Source, XDocument Destination);
}

internal sealed record StyleImportResult(
    IReadOnlyDictionary<string, string> Mapping,
    XDocument? Document,
    IReadOnlySet<string> ImportedIds)
{
    internal static readonly StyleImportResult Empty = new(
        new Dictionary<string, string>(), null, new HashSet<string>());
}
