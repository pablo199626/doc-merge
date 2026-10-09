using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Identity;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxDocumentReader : IDocumentReader
{
    private const int SupportedColumnCount = 3;

    private static readonly IReadOnlyDictionary<XName, string> UnsupportedTags =
        new Dictionary<XName, string>
        {
            [DocxNames.Word + "vMerge"] = "celdas combinadas verticalmente",
            [DocxNames.Word + "gridSpan"] = "celdas combinadas horizontalmente",
            [DocxNames.Word + "tbl"] = "tablas anidadas",
            [DocxNames.Word + "ins"] = "control de cambios",
            [DocxNames.Word + "del"] = "control de cambios",
            [DocxNames.Word + "moveFrom"] = "control de cambios",
            [DocxNames.Word + "moveTo"] = "control de cambios",
            [DocxNames.Word + "commentReference"] = "comentarios",
            [DocxNames.Word + "footnoteReference"] = "notas al pie",
            [DocxNames.Word + "endnoteReference"] = "notas finales",
        };

    public async Task<DocumentSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken)
    {
        ValidatePath(path);
        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            return Parse(path, content, header);
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
            throw new DocumentStructureException($"No se pudo leer el DOCX: {error.Message}", error);
        }
    }

    internal static DocumentSnapshot Parse(string path, byte[] content, string header)
    {
        var package = DocxPackage.FromBytes(content);
        if (!package.Parts.TryGetValue(DocxNames.DocumentPart, out var documentPart))
        {
            throw new DocumentStructureException("El archivo no contiene word/document.xml.");
        }

        var document = DocxXml.Parse(documentPart);
        var location = DocxXml.LocateStrictTable(document, header);
        ValidateTable(location.Table);
        var relationships = ReadRelationships(package);
        var rows = ReadRows(path, package, location, relationships);
        if (rows.Records.Count == 0)
        {
            throw new DocumentStructureException(
                "La tabla encontrada no contiene registros de personas.");
        }

        return new DocumentSnapshot(
            path,
            Convert.ToHexStringLower(SHA256.HashData(content)),
            location.TableIndex,
            location.HeaderIndex,
            location.NameColumnIndex,
            rows.Records,
            rows.AuxiliaryRows);
    }

    private static RowResults ReadRows(
        string path,
        DocxPackage package,
        DocxXml.TableLocation location,
        IReadOnlyDictionary<string, XElement> relationships)
    {
        var rows = location.Table.Elements(DocxNames.Word + "tr").ToArray();
        ValidateHeader(rows[location.HeaderIndex]);
        var records = new List<DocumentRecord>();
        var auxiliaryRows = new List<AuxiliaryRow>();
        for (var index = location.HeaderIndex + 1; index < rows.Length; index++)
        {
            ReadRow(path, package, location, relationships, rows[index], index, records, auxiliaryRows);
        }

        return new RowResults(records, auxiliaryRows);
    }

    private static void ReadRow(
        string path,
        DocxPackage package,
        DocxXml.TableLocation location,
        IReadOnlyDictionary<string, XElement> relationships,
        XElement row,
        int rowIndex,
        ICollection<DocumentRecord> records,
        ICollection<AuxiliaryRow> auxiliaryRows)
    {
        var cells = ValidateCells(row, rowIndex);
        var texts = cells.Select(cell =>
            IdentityNormalizer.NormalizeWhitespace(DocxXml.ElementText(cell))).ToArray();
        if (texts.All(text => text.Length == 0))
        {
            auxiliaryRows.Add(new AuxiliaryRow(rowIndex, SerializeElement(row)));
            return;
        }

        var name = texts[location.NameColumnIndex];
        if (name.Length == 0)
        {
            throw new DocumentStructureException(
                $"La fila {rowIndex} contiene datos, pero no tiene identidad en la columna de nombres.");
        }

        records.Add(CreateRecord(path, package, relationships, row, rowIndex, location, name));
    }

    private static DocumentRecord CreateRecord(
        string path,
        DocxPackage package,
        IReadOnlyDictionary<string, XElement> relationships,
        XElement row,
        int rowIndex,
        DocxXml.TableLocation location,
        string name)
    {
        return new DocumentRecord(
            path,
            location.TableIndex,
            rowIndex,
            name,
            IdentityNormalizer.Normalize(name),
            SerializeElement(row),
            FindDependencies(row, relationships, package.Parts.Keys));
    }

    private static RecordDependencies FindDependencies(
        XElement row,
        IReadOnlyDictionary<string, XElement> relationships,
        IEnumerable<string> packageParts)
    {
        var styles = CollectValues(row, DocxNames.StyleTags, DocxNames.Value);
        var numbering = CollectValues(
            row, new HashSet<XName> { DocxNames.Word + "numId" }, DocxNames.Value);
        var references = CollectRelationshipIds(row)
            .Select(id => ResolveRelationship(id, relationships, packageParts)).ToArray();
        return new RecordDependencies(styles, numbering, references);
    }

    private static IReadOnlyList<string> CollectValues(
        XElement row,
        IReadOnlySet<XName> tags,
        XName attribute)
    {
        return row.DescendantsAndSelf()
            .Where(element => tags.Contains(element.Name))
            .Select(element => element.Attribute(attribute)?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> CollectRelationshipIds(XElement row)
    {
        return row.DescendantsAndSelf().Attributes()
            .Where(attribute => DocxNames.RelationshipAttributes.Contains(attribute.Name))
            .Select(attribute => attribute.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static RelationshipReference ResolveRelationship(
        string id,
        IReadOnlyDictionary<string, XElement> relationships,
        IEnumerable<string> packageParts)
    {
        if (!relationships.TryGetValue(id, out var relationship))
        {
            throw new DocumentStructureException($"La fila contiene una relación no resuelta: {id}.");
        }

        var isExternal = string.Equals(
            relationship.Attribute("TargetMode")?.Value, "external", StringComparison.OrdinalIgnoreCase);
        var originalTarget = relationship.Attribute("Target")?.Value ?? string.Empty;
        var target = isExternal ? originalTarget : PackagePaths.Resolve(DocxNames.DocumentPart, originalTarget);
        if (!isExternal && !packageParts.Contains(target, StringComparer.Ordinal))
        {
            throw new DocumentStructureException(
                $"La relación {id} apunta a una parte inexistente: {target}.");
        }

        return new RelationshipReference(
            id, relationship.Attribute("Type")?.Value ?? string.Empty, target, isExternal);
    }

    private static IReadOnlyDictionary<string, XElement> ReadRelationships(DocxPackage package)
    {
        if (!package.Parts.TryGetValue(DocxNames.RelationshipsPart, out var content))
        {
            return new Dictionary<string, XElement>(StringComparer.Ordinal);
        }

        return DocxXml.Parse(content).Root!
            .Elements(DocxNames.PackageRelationships + "Relationship")
            .Where(element => element.Attribute("Id") is not null)
            .ToDictionary(element => element.Attribute("Id")!.Value, StringComparer.Ordinal);
    }

    private static void ValidateTable(XElement table)
    {
        if (table.Descendants(DocxNames.Word + "tbl").Any())
        {
            throw new DocumentStructureException(
                "La tabla de personas contiene tablas anidadas no admitidas.");
        }

        foreach (var unsupported in UnsupportedTags.Where(pair => pair.Key != DocxNames.Word + "tbl"))
        {
            if (table.Descendants(unsupported.Key).Any())
            {
                throw new DocumentStructureException(
                    $"La tabla de personas contiene {unsupported.Value}, que aún no se admite.");
            }
        }
    }

    private static void ValidateHeader(XElement header)
    {
        if (DocxXml.RowCells(header).Count != SupportedColumnCount)
        {
            throw new DocumentStructureException(
                $"La cabecera tiene {DocxXml.RowCells(header).Count} celdas; el perfil admite exactamente 3.");
        }
    }

    private static IReadOnlyList<XElement> ValidateCells(XElement row, int rowIndex)
    {
        var cells = DocxXml.RowCells(row);
        if (cells.Count != SupportedColumnCount)
        {
            throw new DocumentStructureException(
                $"La fila {rowIndex} tiene {cells.Count} celdas; el perfil admite exactamente 3.");
        }

        return cells;
    }

    private static void ValidatePath(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentStructureException("El documento debe tener extensión .docx.");
        }

        if (!File.Exists(path))
        {
            throw new DocumentStructureException($"No existe el documento DOCX: {path}");
        }
    }

    private static string SerializeElement(XElement element)
    {
        return element.ToString(SaveOptions.DisableFormatting);
    }

    private sealed record RowResults(
        IReadOnlyList<DocumentRecord> Records,
        IReadOnlyList<AuxiliaryRow> AuxiliaryRows);
}
