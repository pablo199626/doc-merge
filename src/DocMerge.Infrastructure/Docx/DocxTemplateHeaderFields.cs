using System.Xml.Linq;
using DocMerge.Application.Documents;

namespace DocMerge.Infrastructure.Docx;

internal static class DocxTemplateHeaderFields
{
    private const string ReservedTagPrefix = "DocMerge.";

    internal static int? Inspect(DocxPackage package, XDocument document, string tableHeader)
    {
        var taggedControls = ReadTaggedControls(package).ToArray();
        var reserved = taggedControls.Where(item =>
            item.Tag.StartsWith(ReservedTagPrefix, StringComparison.Ordinal)).ToArray();
        if (reserved.Length == 0)
        {
            return null;
        }

        var fields = LocateStrict(taggedControls, document, tableHeader);
        ValidateField(fields.ClassTitle, TemplateHeaderContract.ClassTitleTag);
        ValidateField(fields.TutorName, TemplateHeaderContract.TutorNameTag);
        ValidateSharedLocation(fields, document, tableHeader);
        return TemplateHeaderContract.Version;
    }

    internal static HeaderFields LocateStrict(XDocument document, string tableHeader)
    {
        var controls = ReadTaggedControls(DocxNames.DocumentPart, document).ToArray();
        return LocateStrict(controls, document, tableHeader);
    }

    internal static string ReadValue(XElement control) =>
        string.Concat(control.Descendants(DocxNames.Word + "t").Select(item => item.Value));

    internal static void WriteValue(XElement control, string value)
    {
        var textNodes = control.Descendants(DocxNames.Word + "t").ToArray();
        if (textNodes.Length == 0)
        {
            throw new DocumentStructureException("Un campo de cabecera no contiene un nodo de texto editable.");
        }

        textNodes[0].Value = value;
        SetSpacePreservation(textNodes[0], value);
        foreach (var extra in textNodes.Skip(1))
        {
            extra.Value = string.Empty;
            extra.Attribute(XNamespace.Xml + "space")?.Remove();
        }
    }

    internal static void ClearValuesForComparison(XDocument document, string tableHeader)
    {
        var fields = LocateStrict(document, tableHeader);
        WriteValue(fields.ClassTitle, string.Empty);
        WriteValue(fields.TutorName, string.Empty);
    }

    private static HeaderFields LocateStrict(
        IReadOnlyList<TaggedControl> controls,
        XDocument document,
        string tableHeader)
    {
        var title = SelectSingle(controls, TemplateHeaderContract.ClassTitleTag);
        var tutor = SelectSingle(controls, TemplateHeaderContract.TutorNameTag);
        var knownCount = controls.Count(item => item.Tag is
            TemplateHeaderContract.ClassTitleTag or TemplateHeaderContract.TutorNameTag);
        var reservedCount = controls.Count(item =>
            item.Tag.StartsWith(ReservedTagPrefix, StringComparison.Ordinal));
        if (knownCount != reservedCount)
        {
            throw new DocumentStructureException(
                "La plantilla contiene campos DocMerge con una versión o etiqueta no compatible.");
        }

        var fields = new HeaderFields(title.Control, tutor.Control);
        if (title.PartName != DocxNames.DocumentPart || tutor.PartName != DocxNames.DocumentPart)
        {
            throw new DocumentStructureException(
                "Los campos de clase y tutor deben estar en el cuerpo del documento, no en encabezados o pies.");
        }

        ValidateField(fields.ClassTitle, TemplateHeaderContract.ClassTitleTag);
        ValidateField(fields.TutorName, TemplateHeaderContract.TutorNameTag);
        ValidateSharedLocation(fields, document, tableHeader);
        return fields;
    }

    private static TaggedControl SelectSingle(
        IReadOnlyList<TaggedControl> controls,
        string tag)
    {
        var matches = controls.Where(item => item.Tag == tag).ToArray();
        if (matches.Length != 1)
        {
            throw new DocumentStructureException(
                matches.Length == 0
                    ? $"Falta el campo de plantilla {tag}."
                    : $"El campo de plantilla {tag} está duplicado.");
        }

        return matches[0];
    }

    private static void ValidateField(XElement control, string tag)
    {
        var content = control.Element(DocxNames.Word + "sdtContent");
        var parent = control.Parent;
        if (content is null
            || parent?.Name != DocxNames.Word + "p"
            || content.Elements().Any(item => item.Name != DocxNames.Word + "r")
            || !content.Descendants(DocxNames.Word + "t").Any()
            || control.Descendants(DocxNames.Word + "sdt").Any()
            || control.Descendants(DocxNames.Word + "dataBinding").Any()
            || control.Descendants(DocxNames.Word + "placeholder").Any()
            || control.Ancestors().Any(item => item.Name == DocxNames.Word + "txbxContent")
            || control.Ancestors().Any(item => item.Name == DocxNames.Word + "ins" || item.Name == DocxNames.Word + "del"))
        {
            throw new DocumentStructureException(
                $"El campo de plantilla {tag} no es un control de texto simple compatible.");
        }
    }

    private static void ValidateSharedLocation(
        HeaderFields fields,
        XDocument document,
        string tableHeader)
    {
        var titleCell = fields.ClassTitle.Ancestors(DocxNames.Word + "tc").FirstOrDefault();
        var tutorCell = fields.TutorName.Ancestors(DocxNames.Word + "tc").FirstOrDefault();
        var titleTable = fields.ClassTitle.Ancestors(DocxNames.Word + "tbl").FirstOrDefault();
        var tutorTable = fields.TutorName.Ancestors(DocxNames.Word + "tbl").FirstOrDefault();
        var studentTable = DocxXml.LocateStrictTable(document, tableHeader).Table;
        var tables = document.Descendants(DocxNames.Word + "tbl").ToArray();
        if (titleCell is null
            || tutorCell is null
            || !ReferenceEquals(titleCell, tutorCell)
            || titleTable is null
            || !ReferenceEquals(titleTable, tutorTable)
            || Array.IndexOf(tables, titleTable) >= Array.IndexOf(tables, studentTable)
            || fields.ClassTitle.Parent == fields.TutorName.Parent
            || fields.ClassTitle.Ancestors(DocxNames.Word + "tr")
                .First().Descendants(DocxNames.Word + "tblHeader").Any())
        {
            throw new DocumentStructureException(
                "Los campos de clase y tutor deben ocupar párrafos distintos de la misma celda, antes de la tabla de alumnado.");
        }
    }

    private static IEnumerable<TaggedControl> ReadTaggedControls(DocxPackage package)
    {
        foreach (var part in package.Parts.Where(item =>
            item.Key.StartsWith("word/", StringComparison.Ordinal)
            && item.Key.EndsWith(".xml", StringComparison.Ordinal)))
        {
            var document = DocxXml.Parse(part.Value);
            foreach (var control in ReadTaggedControls(part.Key, document))
            {
                yield return control;
            }
        }
    }

    private static IEnumerable<TaggedControl> ReadTaggedControls(string partName, XDocument document)
    {
        foreach (var control in document.Descendants(DocxNames.Word + "sdt"))
        {
            var tag = control.Element(DocxNames.Word + "sdtPr")?
                .Element(DocxNames.Word + "tag")?
                .Attribute(DocxNames.Value)?.Value;
            if (!string.IsNullOrEmpty(tag))
            {
                yield return new TaggedControl(partName, tag, control);
            }
        }
    }

    private static void SetSpacePreservation(XElement text, string value)
    {
        if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
        {
            text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            return;
        }

        text.Attribute(XNamespace.Xml + "space")?.Remove();
    }

    internal sealed record HeaderFields(XElement ClassTitle, XElement TutorName);

    private sealed record TaggedControl(string PartName, string Tag, XElement Control);
}
