using System.Globalization;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Generation;

namespace DocMerge.Infrastructure.Docx;

internal sealed class DocxHistoricalRichContentReader
{
    private const string UnsupportedMessage =
        "Parte del contenido original no puede mostrarse en esta vista previa.";

    private static readonly HashSet<string> UnsupportedElementNames =
    [
        "drawing",
        "object",
        "pict",
        "txbxContent",
        "oMath",
        "oMathPara",
        "fldChar",
        "instrText",
    ];

    private readonly IReadOnlyDictionary<string, XElement> _styles;
    private readonly XElement? _defaultParagraphProperties;
    private readonly XElement? _defaultRunProperties;
    private readonly IReadOnlyDictionary<string, XElement> _numbers;
    private readonly IReadOnlyDictionary<string, XElement> _abstractNumbers;

    internal DocxHistoricalRichContentReader(DocxPackage package)
    {
        var styles = ReadOptionalRoot(package, DocxNames.StylesPart);
        _styles = styles?.Elements(DocxNames.Word + "style")
            .Where(style => style.Attribute(DocxNames.StyleId) is not null)
            .ToDictionary(
                style => style.Attribute(DocxNames.StyleId)!.Value,
                StringComparer.Ordinal)
            ?? new Dictionary<string, XElement>(StringComparer.Ordinal);
        var defaults = styles?.Element(DocxNames.Word + "docDefaults");
        _defaultParagraphProperties = defaults?
            .Element(DocxNames.Word + "pPrDefault")?
            .Element(DocxNames.Word + "pPr");
        _defaultRunProperties = defaults?
            .Element(DocxNames.Word + "rPrDefault")?
            .Element(DocxNames.Word + "rPr");

        var numbering = ReadOptionalRoot(package, DocxNames.NumberingPart);
        _numbers = MapByAttribute(numbering, "num", "numId");
        _abstractNumbers = MapByAttribute(numbering, "abstractNum", "abstractNumId");
    }

    internal HistoricalRichContent Read(XElement cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var paragraphs = cell.Elements(DocxNames.Word + "p")
            .Select(ReadParagraph)
            .ToArray();
        return new HistoricalRichContent(paragraphs);
    }

    private HistoricalRichParagraph ReadParagraph(XElement paragraph)
    {
        var directParagraphProperties = paragraph.Element(DocxNames.Word + "pPr");
        var paragraphStyleId = ValueOf(
            directParagraphProperties?.Element(DocxNames.Word + "pStyle"));
        var paragraphStyle = ResolveStyle(paragraphStyleId);
        var paragraphProperties = MergeProperties(
            ElementsOf(_defaultParagraphProperties),
            paragraphStyle.ParagraphProperties.Values,
            ElementsOf(directParagraphProperties));
        var inheritedRunProperties = MergeProperties(
            ElementsOf(_defaultRunProperties),
            paragraphStyle.RunProperties.Values,
            ElementsOf(directParagraphProperties?.Element(DocxNames.Word + "rPr")));
        var inlines = paragraph.Descendants(DocxNames.Word + "r")
            .SelectMany(run => ReadRun(run, inheritedRunProperties))
            .ToList();
        if (HasUnsupportedContent(paragraph))
        {
            inlines.Add(new HistoricalRichInline(
                HistoricalRichInlineKind.UnsupportedContent,
                UnsupportedMessage,
                HistoricalTextFormatting.Default));
        }

        return new HistoricalRichParagraph(
            inlines,
            ReadParagraphFormatting(paragraphProperties),
            ReadList(paragraphProperties));
    }

    private IEnumerable<HistoricalRichInline> ReadRun(
        XElement run,
        IReadOnlyDictionary<XName, XElement> inheritedProperties)
    {
        var directProperties = run.Element(DocxNames.Word + "rPr");
        var characterStyleId = ValueOf(
            directProperties?.Element(DocxNames.Word + "rStyle"));
        var characterStyle = ResolveStyle(characterStyleId);
        var properties = MergeProperties(
            inheritedProperties.Values,
            characterStyle.RunProperties.Values,
            ElementsOf(directProperties));
        var formatting = ReadTextFormatting(properties);
        foreach (var element in run.Elements().Where(element =>
            element.Name != DocxNames.Word + "rPr"))
        {
            var inline = CreateInline(element, formatting);
            if (inline is not null)
            {
                yield return inline;
            }
        }
    }

    private static HistoricalRichInline? CreateInline(
        XElement element,
        HistoricalTextFormatting formatting)
    {
        if (element.Name == DocxNames.Word + "t")
        {
            return new HistoricalRichInline(HistoricalRichInlineKind.Text, element.Value, formatting);
        }

        if (element.Name.LocalName is "br" or "cr")
        {
            return new HistoricalRichInline(HistoricalRichInlineKind.LineBreak, string.Empty, formatting);
        }

        if (element.Name == DocxNames.Word + "tab")
        {
            return new HistoricalRichInline(HistoricalRichInlineKind.Tab, "\t", formatting);
        }

        return null;
    }

    private ResolvedStyle ResolveStyle(string? styleId)
    {
        if (string.IsNullOrEmpty(styleId))
        {
            return ResolvedStyle.Empty;
        }

        return ResolveStyle(styleId, new HashSet<string>(StringComparer.Ordinal));
    }

    private ResolvedStyle ResolveStyle(string styleId, ISet<string> pending)
    {
        if (!pending.Add(styleId))
        {
            throw new DocumentStructureException(
                $"El documento contiene un ciclo de estilos que incluye {styleId}.");
        }

        if (!_styles.TryGetValue(styleId, out var style))
        {
            throw new DocumentStructureException(
                $"El documento referencia un estilo inexistente: {styleId}.");
        }

        var baseId = ValueOf(style.Element(DocxNames.Word + "basedOn"));
        var inherited = string.IsNullOrEmpty(baseId)
            ? ResolvedStyle.Empty
            : ResolveStyle(baseId, pending);
        pending.Remove(styleId);
        return new ResolvedStyle(
            MergeProperties(
                inherited.ParagraphProperties.Values,
                ElementsOf(style.Element(DocxNames.Word + "pPr"))),
            MergeProperties(
                inherited.RunProperties.Values,
                ElementsOf(style.Element(DocxNames.Word + "rPr"))));
    }

    private HistoricalListDescriptor? ReadList(IReadOnlyDictionary<XName, XElement> properties)
    {
        if (!properties.TryGetValue(DocxNames.Word + "numPr", out var numberProperties))
        {
            return null;
        }

        var numberId = ValueOf(numberProperties.Element(DocxNames.Word + "numId"));
        var levelValue = ValueOf(numberProperties.Element(DocxNames.Word + "ilvl"));
        var level = int.TryParse(levelValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLevel)
            ? parsedLevel
            : 0;
        if (numberId is null || numberId == "0" || !_numbers.TryGetValue(numberId, out var number))
        {
            return null;
        }

        var abstractId = ValueOf(number.Element(DocxNames.Word + "abstractNumId"));
        if (abstractId is null || !_abstractNumbers.TryGetValue(abstractId, out var abstractNumber))
        {
            return null;
        }

        var levelElement = abstractNumber.Elements(DocxNames.Word + "lvl")
            .FirstOrDefault(item => string.Equals(
                item.Attribute(DocxNames.Word + "ilvl")?.Value,
                level.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
        var format = ValueOf(levelElement?.Element(DocxNames.Word + "numFmt")) ?? "bullet";
        var levelText = ValueOf(levelElement?.Element(DocxNames.Word + "lvlText"))
            ?? (format == "bullet" ? "•" : "%1.");
        return new HistoricalListDescriptor(level, format, levelText);
    }

    private static HistoricalTextFormatting ReadTextFormatting(
        IReadOnlyDictionary<XName, XElement> properties)
    {
        var fonts = Get(properties, "rFonts");
        var font = AttributeValue(fonts, "ascii")
            ?? AttributeValue(fonts, "hAnsi")
            ?? AttributeValue(fonts, "eastAsia")
            ?? AttributeValue(fonts, "cs");
        var size = ParseHalfPoints(ValueOf(Get(properties, "sz")));
        var color = NormalizeColor(ValueOf(Get(properties, "color")));
        var highlight = ValueOf(Get(properties, "highlight"))
            ?? NormalizeColor(AttributeValue(Get(properties, "shd"), "fill"));
        return new HistoricalTextFormatting(
            ReadBoolean(Get(properties, "b")),
            ReadBoolean(Get(properties, "i")),
            ReadUnderline(Get(properties, "u")),
            font,
            size,
            color,
            highlight);
    }

    private static HistoricalParagraphFormatting ReadParagraphFormatting(
        IReadOnlyDictionary<XName, XElement> properties)
    {
        var indentation = Get(properties, "ind");
        var spacing = Get(properties, "spacing");
        return new HistoricalParagraphFormatting(
            ParseAlignment(ValueOf(Get(properties, "jc"))),
            ParseTwips(AttributeValue(indentation, "left") ?? AttributeValue(indentation, "start")),
            ParseTwips(AttributeValue(indentation, "right") ?? AttributeValue(indentation, "end")),
            ParseFirstLineIndent(indentation),
            ParseTwips(AttributeValue(spacing, "before")),
            ParseTwips(AttributeValue(spacing, "after")));
    }

    private static HistoricalParagraphAlignment ParseAlignment(string? value) => value switch
    {
        "center" => HistoricalParagraphAlignment.Center,
        "right" or "end" => HistoricalParagraphAlignment.Right,
        "both" or "distribute" => HistoricalParagraphAlignment.Justify,
        _ => HistoricalParagraphAlignment.Left,
    };

    private static double ParseFirstLineIndent(XElement? indentation)
    {
        var hanging = ParseTwips(AttributeValue(indentation, "hanging"));
        if (hanging > 0)
        {
            return -hanging;
        }

        return ParseTwips(AttributeValue(indentation, "firstLine"));
    }

    private static double ParseTwips(string? value) =>
        double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var twips)
            ? twips / 20d
            : 0;

    private static double? ParseHalfPoints(string? value) =>
        double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var halfPoints)
            ? halfPoints / 2d
            : null;

    private static bool ReadBoolean(XElement? element)
    {
        if (element is null)
        {
            return false;
        }

        return ValueOf(element) is not ("0" or "false" or "off");
    }

    private static bool ReadUnderline(XElement? element) =>
        element is not null && ValueOf(element) is not ("0" or "false" or "off" or "none");

    private static string? NormalizeColor(string? value)
    {
        if (value is null || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Length == 6 && value.All(Uri.IsHexDigit) ? value.ToUpperInvariant() : null;
    }

    private static bool HasUnsupportedContent(XElement paragraph) => paragraph.Descendants()
        .Any(element => UnsupportedElementNames.Contains(element.Name.LocalName));

    private static XElement? Get(IReadOnlyDictionary<XName, XElement> values, string name) =>
        values.GetValueOrDefault(DocxNames.Word + name);

    private static string? ValueOf(XElement? element) =>
        element?.Attribute(DocxNames.Value)?.Value;

    private static string? AttributeValue(XElement? element, string name) =>
        element?.Attribute(DocxNames.Word + name)?.Value;

    private static IReadOnlyDictionary<XName, XElement> MergeProperties(
        params IEnumerable<XElement>[] sources)
    {
        var result = new Dictionary<XName, XElement>();
        foreach (var source in sources)
        {
            foreach (var property in source)
            {
                result[property.Name] = property;
            }
        }

        return result;
    }

    private static IEnumerable<XElement> ElementsOf(XElement? parent) =>
        parent?.Elements() ?? [];

    private static IReadOnlyDictionary<string, XElement> MapByAttribute(
        XElement? root,
        string elementName,
        string attributeName)
    {
        if (root is null)
        {
            return new Dictionary<string, XElement>(StringComparer.Ordinal);
        }

        var attribute = DocxNames.Word + attributeName;
        return root.Elements(DocxNames.Word + elementName)
            .Where(element => element.Attribute(attribute) is not null)
            .ToDictionary(element => element.Attribute(attribute)!.Value, StringComparer.Ordinal);
    }

    private static XElement? ReadOptionalRoot(DocxPackage package, string part) =>
        package.Parts.TryGetValue(part, out var content)
            ? DocxXml.Parse(content).Root
            : null;

    private sealed record ResolvedStyle(
        IReadOnlyDictionary<XName, XElement> ParagraphProperties,
        IReadOnlyDictionary<XName, XElement> RunProperties)
    {
        internal static ResolvedStyle Empty { get; } = new(
            new Dictionary<XName, XElement>(),
            new Dictionary<XName, XElement>());
    }
}
