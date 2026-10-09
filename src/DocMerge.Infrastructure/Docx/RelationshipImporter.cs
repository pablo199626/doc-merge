using System.Xml.Linq;
using DocMerge.Application.Documents;

namespace DocMerge.Infrastructure.Docx;

internal sealed class RelationshipImporter
{
    private readonly DocxPackage _source;
    private readonly DocxPackage _destination;
    private readonly Dictionary<string, string> _partMapping = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedParts;

    private RelationshipImporter(DocxPackage source, DocxPackage destination)
    {
        _source = source;
        _destination = destination;
        _usedParts = destination.Parts.Keys.ToHashSet(StringComparer.Ordinal);
    }

    internal static RelationshipImportResult Import(
        DocxPackage source,
        DocxPackage destination,
        IReadOnlyList<XElement> rows)
    {
        var sourceDocument = ReadRelationships(source);
        var destinationDocument = ReadRelationships(destination);
        var sourceRelationships = MapRelationships(sourceDocument.Root!);
        var usedIds = MapRelationships(destinationDocument.Root!).Keys
            .ToHashSet(StringComparer.Ordinal);
        var importer = new RelationshipImporter(source, destination);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in CollectRelationshipIds(rows).Order(StringComparer.Ordinal))
        {
            ImportRelationship(id, sourceRelationships, destinationDocument.Root!, usedIds, importer, mapping);
        }

        return new RelationshipImportResult(mapping, destinationDocument);
    }

    private static void ImportRelationship(
        string id,
        IReadOnlyDictionary<string, XElement> sourceRelationships,
        XElement destinationRoot,
        ISet<string> usedIds,
        RelationshipImporter importer,
        IDictionary<string, string> mapping)
    {
        if (!sourceRelationships.TryGetValue(id, out var sourceRelationship))
        {
            throw new DocumentStructureException($"El origen no define la relación {id}.");
        }

        var clone = new XElement(sourceRelationship);
        var newId = NextRelationshipId(usedIds);
        clone.SetAttributeValue("Id", newId);
        if (!IsExternal(clone))
        {
            var sourcePath = PackagePaths.Resolve(
                DocxNames.DocumentPart, clone.Attribute("Target")?.Value ?? string.Empty);
            var destinationPath = importer.ImportPart(sourcePath);
            clone.SetAttributeValue("Target", PackagePaths.RelativeTo("word", destinationPath));
        }

        destinationRoot.Add(clone);
        mapping.Add(id, newId);
    }

    private string ImportPart(string sourcePath)
    {
        if (_partMapping.TryGetValue(sourcePath, out var existing))
        {
            return existing;
        }

        if (!_source.Parts.TryGetValue(sourcePath, out var content))
        {
            throw new DocumentStructureException(
                $"El origen no contiene la parte relacionada: {sourcePath}.");
        }

        var destinationPath = CreateUniquePartName(sourcePath);
        _partMapping.Add(sourcePath, destinationPath);
        ImportPartRelationships(sourcePath, destinationPath);
        _destination.Parts[destinationPath] = content;
        CopyContentType(sourcePath, destinationPath);
        return destinationPath;
    }

    private void ImportPartRelationships(string sourcePath, string destinationPath)
    {
        var relationshipsPath = PackagePaths.RelationshipsPath(sourcePath);
        if (!_source.Parts.TryGetValue(relationshipsPath, out var content))
        {
            return;
        }

        var document = DocxXml.Parse(content);
        foreach (var relationship in document.Root!
            .Elements(DocxNames.PackageRelationships + "Relationship")
            .Where(relationship => !IsExternal(relationship)))
        {
            var target = PackagePaths.Resolve(
                sourcePath, relationship.Attribute("Target")?.Value ?? string.Empty);
            var imported = ImportPart(target);
            relationship.SetAttributeValue(
                "Target", PackagePaths.RelativeTo(PackagePaths.Directory(destinationPath), imported));
        }

        _destination.Parts[PackagePaths.RelationshipsPath(destinationPath)] = DocxXml.Serialize(document);
    }

    private void CopyContentType(string sourcePath, string destinationPath)
    {
        if (!_source.Parts.TryGetValue(DocxNames.ContentTypesPart, out var sourceContent)
            || !_destination.Parts.TryGetValue(DocxNames.ContentTypesPart, out var destinationContent))
        {
            return;
        }

        var sourceDocument = DocxXml.Parse(sourceContent);
        var destinationDocument = DocxXml.Parse(destinationContent);
        var sourceOverride = sourceDocument.Root!
            .Elements(DocxNames.ContentTypes + "Override")
            .FirstOrDefault(element => element.Attribute("PartName")?.Value == $"/{sourcePath}");
        if (sourceOverride is not null)
        {
            CopyOverride(sourceOverride, destinationPath, destinationDocument.Root!);
        }
        else
        {
            CopyDefault(sourcePath, sourceDocument.Root!, destinationDocument.Root!);
        }

        _destination.Parts[DocxNames.ContentTypesPart] = DocxXml.Serialize(destinationDocument);
    }

    private static void CopyOverride(
        XElement sourceOverride,
        string destinationPath,
        XElement destinationRoot)
    {
        var target = $"/{destinationPath}";
        if (destinationRoot.Elements(DocxNames.ContentTypes + "Override")
            .Any(element => element.Attribute("PartName")?.Value == target))
        {
            return;
        }

        var clone = new XElement(sourceOverride);
        clone.SetAttributeValue("PartName", target);
        destinationRoot.Add(clone);
    }

    private static void CopyDefault(
        string sourcePath,
        XElement sourceRoot,
        XElement destinationRoot)
    {
        var extension = Path.GetExtension(sourcePath).TrimStart('.');
        var sourceDefault = sourceRoot.Elements(DocxNames.ContentTypes + "Default")
            .FirstOrDefault(element => string.Equals(
                element.Attribute("Extension")?.Value, extension, StringComparison.OrdinalIgnoreCase));
        var exists = destinationRoot.Elements(DocxNames.ContentTypes + "Default")
            .Any(element => string.Equals(
                element.Attribute("Extension")?.Value, extension, StringComparison.OrdinalIgnoreCase));
        if (sourceDefault is not null && !exists)
        {
            destinationRoot.Add(new XElement(sourceDefault));
        }
    }

    private string CreateUniquePartName(string path)
    {
        if (_usedParts.Add(path))
        {
            return path;
        }

        var directory = PackagePaths.Directory(path);
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var extension = Path.GetExtension(fileName);
        var stem = fileName[..^extension.Length];
        for (var number = 1; ; number++)
        {
            var candidate = $"{directory}/docmerge_{number}_{stem}{extension}".TrimStart('/');
            if (_usedParts.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static XDocument ReadRelationships(DocxPackage package)
    {
        if (package.Parts.TryGetValue(DocxNames.RelationshipsPart, out var content))
        {
            return DocxXml.Parse(content);
        }

        return new XDocument(new XElement(DocxNames.PackageRelationships + "Relationships"));
    }

    private static Dictionary<string, XElement> MapRelationships(XElement root)
    {
        return root.Elements(DocxNames.PackageRelationships + "Relationship")
            .Where(element => element.Attribute("Id") is not null)
            .ToDictionary(element => element.Attribute("Id")!.Value, StringComparer.Ordinal);
    }

    private static HashSet<string> CollectRelationshipIds(IReadOnlyList<XElement> rows)
    {
        return rows.SelectMany(row => row.DescendantsAndSelf().Attributes())
            .Where(attribute => DocxNames.RelationshipAttributes.Contains(attribute.Name))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string NextRelationshipId(ISet<string> usedIds)
    {
        for (var number = 1; ; number++)
        {
            var candidate = $"rId{number}";
            if (usedIds.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool IsExternal(XElement relationship)
    {
        return string.Equals(
            relationship.Attribute("TargetMode")?.Value, "external", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record RelationshipImportResult(
    IReadOnlyDictionary<string, string> Mapping,
    XDocument Document);
