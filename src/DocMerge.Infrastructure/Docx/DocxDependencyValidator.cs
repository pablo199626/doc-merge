using System.Security.Cryptography;
using System.Xml.Linq;
using DocMerge.Application.Validation;
using DocMerge.Domain.Documents;

namespace DocMerge.Infrastructure.Docx;

internal static class DocxDependencyValidator
{
    private static readonly HashSet<string> AllowedRootChanges =
    [
        DocxNames.DocumentPart,
        DocxNames.StylesPart,
        DocxNames.NumberingPart,
        DocxNames.RelationshipsPart,
        DocxNames.ContentTypesPart,
    ];

    internal static void Validate(
        DependencyValidationContext context,
        IReadOnlyList<DocumentRecord> expectedRecords,
        IReadOnlyList<DocumentRecord> actualRecords,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < expectedRecords.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedParts = SelectExpectedParts(expectedRecords[index], context);
            ValidateRecordDependencies(
                expectedRecords[index], actualRecords[index], expectedParts, context.ResultParts);
        }

        ValidateAddedParts(
            context.Destination.Parts, context.ResultParts, actualRecords, cancellationToken);
    }

    private static IReadOnlyDictionary<string, byte[]> SelectExpectedParts(
        DocumentRecord record,
        DependencyValidationContext context)
    {
        var recordPath = Path.GetFullPath(record.DocumentPath);
        if (string.Equals(
            recordPath, Path.GetFullPath(context.Source.Path), StringComparison.OrdinalIgnoreCase))
        {
            return context.Source.Parts;
        }

        if (string.Equals(
            recordPath,
            Path.GetFullPath(context.Destination.Path),
            StringComparison.OrdinalIgnoreCase))
        {
            return context.Destination.Parts;
        }

        throw new TransferValidationException(
            $"No se reconoce la procedencia de la fila {record.Location}: {record.DocumentPath}.");
    }

    private static void ValidateRecordDependencies(
        DocumentRecord expected,
        DocumentRecord actual,
        IReadOnlyDictionary<string, byte[]> expectedParts,
        IReadOnlyDictionary<string, byte[]> actualParts)
    {
        var expectedRow = XElement.Parse(expected.RowXml, LoadOptions.PreserveWhitespace);
        var actualRow = XElement.Parse(actual.RowXml, LoadOptions.PreserveWhitespace);
        ValidateSignatures(
            expected.OriginalName,
            "estilos",
            CollectStyleIds(expectedRow).Select(id => StyleSignature(expectedParts, id)),
            CollectStyleIds(actualRow).Select(id => StyleSignature(actualParts, id)));
        ValidateSignatures(
            expected.OriginalName,
            "numeraciones",
            CollectNumberingIds(expectedRow).Select(id => NumberingSignature(expectedParts, id)),
            CollectNumberingIds(actualRow).Select(id => NumberingSignature(actualParts, id)));
        ValidateRelationshipSignatures(expected, actual, expectedParts, actualParts);
    }

    private static IEnumerable<string> CollectStyleIds(XElement row)
    {
        return row.DescendantsAndSelf()
            .Where(element => DocxNames.StyleTags.Contains(element.Name))
            .Select(element => element.Attribute(DocxNames.Value)?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!);
    }

    private static IEnumerable<string> CollectNumberingIds(XElement row)
    {
        return row.DescendantsAndSelf(DocxNames.Word + "numId")
            .Select(element => element.Attribute(DocxNames.Value)?.Value)
            .Where(value => value is not null and not "0")
            .Select(value => value!);
    }

    private static void ValidateSignatures(
        string name,
        string dependencyType,
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new TransferValidationException(
                $"Las {dependencyType} de {name} no coinciden con su procedencia.");
        }
    }

    private static string StyleSignature(
        IReadOnlyDictionary<string, byte[]> parts,
        string id)
    {
        return StyleSignature(parts, id, new HashSet<string>(StringComparer.Ordinal));
    }

    private static string StyleSignature(
        IReadOnlyDictionary<string, byte[]> parts,
        string id,
        ISet<string> visiting)
    {
        if (!visiting.Add(id))
        {
            return "STYLE_CYCLE";
        }

        var style = FindElementById(parts, DocxNames.StylesPart, "style", DocxNames.StyleId, id);
        var clone = new XElement(style);
        clone.SetAttributeValue(DocxNames.StyleId, "STYLE");
        var dependencies = new List<string>();
        foreach (var name in new[] { "basedOn", "link" })
        {
            var attribute = clone.Element(DocxNames.Word + name)?.Attribute(DocxNames.Value);
            if (attribute is null)
            {
                continue;
            }

            var linkedId = attribute.Value;
            attribute.Value = "LINKED_STYLE";
            dependencies.Add(StyleSignature(parts, linkedId, visiting));
        }

        foreach (var number in clone.Descendants(DocxNames.Word + "numId"))
        {
            var attribute = number.Attribute(DocxNames.Value);
            if (attribute is null || attribute.Value == "0")
            {
                continue;
            }

            var numberId = attribute.Value;
            attribute.Value = "NUMBERING";
            dependencies.Add(NumberingSignature(parts, numberId));
        }

        visiting.Remove(id);
        return clone.ToString(SaveOptions.DisableFormatting)
            + string.Join('|', dependencies);
    }

    private static string NumberingSignature(
        IReadOnlyDictionary<string, byte[]> parts,
        string id)
    {
        var number = FindElementById(
            parts, DocxNames.NumberingPart, "num", DocxNames.Word + "numId", id);
        var abstractId = number.Element(DocxNames.Word + "abstractNumId")
            ?.Attribute(DocxNames.Value)?.Value
            ?? throw new TransferValidationException(
                $"La numeración {id} no contiene una definición abstracta.");
        var abstractNumber = FindElementById(
            parts,
            DocxNames.NumberingPart,
            "abstractNum",
            DocxNames.Word + "abstractNumId",
            abstractId);
        var numberClone = new XElement(number);
        numberClone.SetAttributeValue(DocxNames.Word + "numId", "NUMBERING");
        numberClone.Element(DocxNames.Word + "abstractNumId")!
            .SetAttributeValue(DocxNames.Value, "ABSTRACT");
        var abstractClone = new XElement(abstractNumber);
        abstractClone.SetAttributeValue(DocxNames.Word + "abstractNumId", "ABSTRACT");
        return numberClone.ToString(SaveOptions.DisableFormatting)
            + abstractClone.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement FindElementById(
        IReadOnlyDictionary<string, byte[]> parts,
        string partName,
        string elementName,
        XName attributeName,
        string id)
    {
        if (!parts.TryGetValue(partName, out var content))
        {
            throw new TransferValidationException($"Falta la parte requerida {partName}.");
        }

        return DocxXml.Parse(content).Root!
            .Elements(DocxNames.Word + elementName)
            .SingleOrDefault(element => element.Attribute(attributeName)?.Value == id)
            ?? throw new TransferValidationException(
                $"La parte {partName} no define la referencia {id}.");
    }

    private static void ValidateRelationshipSignatures(
        DocumentRecord expected,
        DocumentRecord actual,
        IReadOnlyDictionary<string, byte[]> expectedParts,
        IReadOnlyDictionary<string, byte[]> actualParts)
    {
        var expectedReferences = expected.Dependencies.Relationships
            .ToDictionary(reference => reference.RelationshipId, StringComparer.Ordinal);
        var actualReferences = actual.Dependencies.Relationships
            .ToDictionary(reference => reference.RelationshipId, StringComparer.Ordinal);
        var expectedIds = CollectRelationshipIds(expected.RowXml);
        var actualIds = CollectRelationshipIds(actual.RowXml);
        if (expectedIds.Count != actualIds.Count)
        {
            throw new TransferValidationException(
                $"Las relaciones de {expected.OriginalName} no coinciden con su procedencia.");
        }

        for (var index = 0; index < expectedIds.Count; index++)
        {
            var expectedSignature = RelationshipSignature(
                expectedParts, expectedReferences[expectedIds[index]]);
            var actualSignature = RelationshipSignature(
                actualParts, actualReferences[actualIds[index]]);
            if (!string.Equals(expectedSignature, actualSignature, StringComparison.Ordinal))
            {
                throw new TransferValidationException(
                    $"Las relaciones de {expected.OriginalName} no coinciden con su procedencia.");
            }
        }
    }

    private static IReadOnlyList<string> CollectRelationshipIds(string rowXml)
    {
        return XElement.Parse(rowXml, LoadOptions.PreserveWhitespace)
            .DescendantsAndSelf()
            .Attributes()
            .Where(attribute => DocxNames.RelationshipAttributes.Contains(attribute.Name))
            .Select(attribute => attribute.Value)
            .ToArray();
    }

    private static string RelationshipSignature(
        IReadOnlyDictionary<string, byte[]> parts,
        RelationshipReference reference)
    {
        if (reference.IsExternal)
        {
            return $"{reference.RelationshipType}|external|{reference.Target}";
        }

        return $"{reference.RelationshipType}|internal|{PartSignature(
            parts, reference.Target, new HashSet<string>(StringComparer.Ordinal))}";
    }

    private static string PartSignature(
        IReadOnlyDictionary<string, byte[]> parts,
        string partPath,
        ISet<string> visiting)
    {
        if (!visiting.Add(partPath))
        {
            return "PART_CYCLE";
        }

        if (!parts.TryGetValue(partPath, out var content))
        {
            throw new TransferValidationException($"Falta la parte relacionada {partPath}.");
        }

        var relationships = new List<string>();
        var relationshipsPath = PackagePaths.RelationshipsPath(partPath);
        if (parts.TryGetValue(relationshipsPath, out var relationshipContent))
        {
            var elements = DocxXml.Parse(relationshipContent).Root!
                .Elements(DocxNames.PackageRelationships + "Relationship");
            foreach (var relationship in elements)
            {
                var type = relationship.Attribute("Type")?.Value ?? string.Empty;
                var target = relationship.Attribute("Target")?.Value ?? string.Empty;
                if (IsExternal(relationship))
                {
                    relationships.Add($"{type}|external|{target}");
                    continue;
                }

                var resolved = PackagePaths.Resolve(partPath, target);
                relationships.Add($"{type}|internal|{PartSignature(parts, resolved, visiting)}");
            }
        }

        visiting.Remove(partPath);
        relationships.Sort(StringComparer.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(content))
            + string.Join('|', relationships);
    }

    private static void ValidateAddedParts(
        IReadOnlyDictionary<string, byte[]> destinationParts,
        IReadOnlyDictionary<string, byte[]> resultParts,
        IReadOnlyList<DocumentRecord> actualRecords,
        CancellationToken cancellationToken)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in actualRecords.SelectMany(
            record => record.Dependencies.Relationships).Where(reference => !reference.IsExternal))
        {
            CollectReachableParts(resultParts, reference.Target, reachable, cancellationToken);
        }

        var unnecessary = resultParts.Keys.Where(part => !destinationParts.ContainsKey(part))
            .Where(part => !AllowedRootChanges.Contains(part) && !reachable.Contains(part))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unnecessary.Length > 0)
        {
            throw new TransferValidationException(
                $"El resultado contiene partes añadidas innecesarias: [{string.Join(", ", unnecessary)}].");
        }
    }

    private static void CollectReachableParts(
        IReadOnlyDictionary<string, byte[]> parts,
        string partPath,
        ISet<string> reachable,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!reachable.Add(partPath))
        {
            return;
        }

        var relationshipsPath = PackagePaths.RelationshipsPath(partPath);
        if (!parts.TryGetValue(relationshipsPath, out var content))
        {
            return;
        }

        reachable.Add(relationshipsPath);
        var relationships = DocxXml.Parse(content).Root!
            .Elements(DocxNames.PackageRelationships + "Relationship")
            .Where(relationship => !IsExternal(relationship));
        foreach (var relationship in relationships)
        {
            var target = relationship.Attribute("Target")?.Value ?? string.Empty;
            CollectReachableParts(
                parts, PackagePaths.Resolve(partPath, target), reachable, cancellationToken);
        }
    }

    private static bool IsExternal(XElement relationship)
    {
        return string.Equals(
            relationship.Attribute("TargetMode")?.Value, "external", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed record DependencyValidationContext(
        DocumentPackage Source,
        DocumentPackage Destination,
        IReadOnlyDictionary<string, byte[]> ResultParts);

    internal sealed record DocumentPackage(
        string Path,
        IReadOnlyDictionary<string, byte[]> Parts);
}
