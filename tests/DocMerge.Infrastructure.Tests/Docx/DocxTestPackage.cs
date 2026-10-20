using System.IO.Compression;

namespace DocMerge.Infrastructure.Tests.Docx;

internal static class DocxTestPackage
{
    internal const string WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal const string RelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal const string Header =
        "<w:tr><w:tc><w:p/></w:tc><w:tc><w:p><w:r><w:t>Nombre y Apellidos</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Medidas</w:t></w:r></w:p></w:tc></w:tr>";

    internal const string EmptyRow =
        "<w:tr><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc><w:tc><w:p/></w:tc></w:tr>";

    internal static string PersonRow(string name, string measure, string extra = "")
    {
        return $"<w:tr>{extra}<w:tc><w:p/></w:tc><w:tc><w:p><w:pPr><w:pStyle w:val=\"EstiloPersona\"/></w:pPr><w:r><w:t>{name}</w:t></w:r></w:p></w:tc><w:tc><w:p><w:pPr><w:pStyle w:val=\"EstiloPersona\"/><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"7\"/></w:numPr></w:pPr><w:hyperlink r:id=\"rId9\"><w:r><w:t>{measure}</w:t></w:r></w:hyperlink></w:p></w:tc></w:tr>";
    }

    internal static void Create(
        string path,
        IReadOnlyList<string> tables,
        string marker = "DESTINATION",
        bool source = false,
        bool documentHeaderFields = false,
        bool omitTutorField = false,
        bool duplicateClassTitleField = false,
        bool invalidCompatibilityPrefix = false)
    {
        var documentHeader = documentHeaderFields
            ? DocumentHeaderFields(omitTutorField, duplicateClassTitleField)
            : string.Empty;
        var compatibility = invalidCompatibilityPrefix
            ? " xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"w14\""
            : string.Empty;
        var document = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"{WordNamespace}\" xmlns:r=\"{RelationshipNamespace}\"{compatibility}><w:body><w:p><w:r><w:t>{marker}</w:t></w:r></w:p>{documentHeader}{string.Join(string.Empty, tables.Select(rows => $"<w:tbl>{rows}</w:tbl>"))}<w:sectPr/></w:body></w:document>";
        var color = source ? "FF0000" : "0000FF";
        var styles = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:styles xmlns:w=\"{WordNamespace}\"><w:style w:type=\"paragraph\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:rPr><w:color w:val=\"{color}\"/></w:rPr></w:style><w:style w:type=\"paragraph\" w:styleId=\"EstiloPersona\"><w:name w:val=\"Persona\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:numPr><w:numId w:val=\"7\"/></w:numPr></w:pPr></w:style></w:styles>";
        var numbering = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:numbering xmlns:w=\"{WordNamespace}\"><w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"{(source ? "%1." : "(%1)")}\"/></w:lvl></w:abstractNum><w:num w:numId=\"7\"><w:abstractNumId w:val=\"1\"/></w:num></w:numbering>";
        var resourceRelationship = source
            ? "<Relationship Id=\"rId10\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/source.bin\"/>"
            : string.Empty;
        var relationships = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId9\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"https://{marker}.invalid\" TargetMode=\"External\"/>{resourceRelationship}</Relationships>";
        var contentTypes = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>{(source ? "<Default Extension=\"bin\" ContentType=\"application/octet-stream\"/>" : string.Empty)}</Types>";
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "[Content_Types].xml", contentTypes);
        Write(archive, "word/document.xml", document);
        Write(archive, "word/styles.xml", styles);
        Write(archive, "word/numbering.xml", numbering);
        Write(archive, "word/_rels/document.xml.rels", relationships);
        Write(archive, "custom/marker.txt", marker);
        if (source)
        {
            Write(archive, "word/media/source.bin", "SOURCE-RESOURCE");
        }
    }

    private static string DocumentHeaderFields(bool omitTutorField, bool duplicateClassTitleField) =>
        "<w:tbl><w:tr><w:tc>"
        + HeaderField("DocMerge.ClassTitle")
        + (duplicateClassTitleField ? HeaderField("DocMerge.ClassTitle") : string.Empty)
        + (omitTutorField ? string.Empty : HeaderField("DocMerge.TutorName"))
        + "<w:p/></w:tc><w:tc><w:p><w:r><w:t>Contenido fijo</w:t></w:r></w:p></w:tc>"
        + "</w:tr></w:tbl>";

    private static string HeaderField(string tag) =>
        $"<w:p><w:sdt><w:sdtPr><w:tag w:val=\"{tag}\"/><w:text/></w:sdtPr>"
        + "<w:sdtContent><w:r><w:rPr><w:b/></w:rPr><w:t/></w:r></w:sdtContent>"
        + "</w:sdt></w:p>";

    internal static Dictionary<string, byte[]> ReadParts(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            },
            StringComparer.Ordinal);
    }

    internal static void ReplacePart(string path, string partName, string content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.GetEntry(partName)?.Delete();
        Write(archive, partName, content);
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
