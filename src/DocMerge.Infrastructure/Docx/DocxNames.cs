using System.Xml.Linq;

namespace DocMerge.Infrastructure.Docx;

internal static class DocxNames
{
    internal const string DocumentPart = "word/document.xml";
    internal const string StylesPart = "word/styles.xml";
    internal const string NumberingPart = "word/numbering.xml";
    internal const string RelationshipsPart = "word/_rels/document.xml.rels";
    internal const string ContentTypesPart = "[Content_Types].xml";

    internal static readonly XNamespace Word =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal static readonly XNamespace OfficeRelationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static readonly XNamespace PackageRelationships =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    internal static readonly XNamespace ContentTypes =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    internal static readonly XNamespace MarkupCompatibility =
        "http://schemas.openxmlformats.org/markup-compatibility/2006";

    internal static readonly XName Value = Word + "val";
    internal static readonly XName StyleId = Word + "styleId";

    internal static readonly HashSet<XName> StyleTags =
    [
        Word + "pStyle",
        Word + "rStyle",
        Word + "tblStyle",
    ];

    internal static readonly HashSet<XName> RelationshipAttributes =
    [
        OfficeRelationships + "id",
        OfficeRelationships + "embed",
        OfficeRelationships + "link",
    ];
}
