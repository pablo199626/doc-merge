using System.Globalization;
using System.Text.RegularExpressions;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Identity;

namespace DocMerge.Infrastructure.Pdf;

public sealed partial class GroupRosterPageParser
{
    public ParsedGroupRoster Parse(IReadOnlyList<string> pageTexts, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pageTexts);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (pageTexts.Count == 0 || pageTexts.All(string.IsNullOrWhiteSpace))
        {
            throw new PdfTextMissingException(
                "El PDF de clase no contiene texto extraíble; puede necesitar OCR.");
        }

        var classIdentifier = ReadClassIdentifier(pageTexts, fileName);
        var entries = ReadEntries(pageTexts, classIdentifier);
        ValidateOrdinals(entries);
        return new ParsedGroupRoster(classIdentifier, entries);
    }

    private static ClassIdentifier ReadClassIdentifier(
        IReadOnlyList<string> pageTexts,
        string fileName)
    {
        var headerClasses = pageTexts.SelectMany(ExtractHeaderClasses)
            .Distinct()
            .ToArray();
        if (headerClasses.Length != 1)
        {
            throw new PdfStructureException(
                headerClasses.Length == 0
                    ? "No se encontró una cabecera GRUPO identificable en el PDF."
                    : "El PDF contiene cabeceras GRUPO contradictorias.");
        }

        var fileClass = ExtractFileClass(fileName);
        if (fileClass is not null && fileClass != headerClasses[0])
        {
            throw new PdfStructureException(
                $"La clase {headerClasses[0].CanonicalCode} indicada por el PDF no coincide " +
                $"con {fileClass.CanonicalCode}, indicada por el nombre de archivo.");
        }

        return headerClasses[0];
    }

    private static IEnumerable<ClassIdentifier> ExtractHeaderClasses(string pageText)
    {
        return GroupHeaderRegex().Matches(pageText)
            .Select(match => CreateIdentifier(match));
    }

    private static ClassIdentifier? ExtractFileClass(string fileName)
    {
        var matches = FileClassRegex().Matches(Path.GetFileNameWithoutExtension(fileName));
        if (matches.Count == 0)
        {
            return null;
        }

        var identifiers = matches.Select(match => CreateIdentifier(match)).Distinct().ToArray();
        if (identifiers.Length != 1)
        {
            throw new PdfStructureException("El nombre del PDF contiene clases contradictorias.");
        }

        return identifiers[0];
    }

    private static ClassIdentifier CreateIdentifier(Match match)
    {
        var level = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return new ClassIdentifier("ESO", level, match.Groups[2].Value);
    }

    private static IReadOnlyList<ClassRosterEntry> ReadEntries(
        IReadOnlyList<string> pageTexts,
        ClassIdentifier classIdentifier)
    {
        var entries = new List<ClassRosterEntry>();
        for (var pageIndex = 0; pageIndex < pageTexts.Count; pageIndex++)
        {
            var normalized = NormalizePage(pageTexts[pageIndex]);
            foreach (Match match in EntryRegex().Matches(normalized))
            {
                entries.Add(CreateEntry(match, pageIndex + 1, classIdentifier));
            }
        }

        if (entries.Count == 0)
        {
            throw new PdfStructureException(
                "No se extrajo ninguna fila completa de alumnado del PDF de clase.");
        }

        return entries;
    }

    private static ClassRosterEntry CreateEntry(
        Match match,
        int pageNumber,
        ClassIdentifier classIdentifier)
    {
        var ordinal = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var studentNumber = match.Groups[2].Value;
        var name = IdentityNormalizer.NormalizeWhitespace(match.Groups[3].Value);
        var course = match.Groups[5].Value;
        if (!string.Equals(
            course,
            $"{classIdentifier.Level}{classIdentifier.Stage}",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfStructureException(
                $"La entrada {ordinal} de la página {pageNumber} pertenece al curso {course}, " +
                $"no a {classIdentifier.Level}{classIdentifier.Stage}.");
        }

        return new ClassRosterEntry(
            name,
            IdentityNormalizer.Normalize(name),
            StudentNameMatchKey.Create(name),
            studentNumber,
            pageNumber,
            ordinal);
    }

    private static string NormalizePage(string pageText)
    {
        var lines = LineBreakRegex().Split(pageText)
            .Select(IdentityNormalizer.NormalizeWhitespace);
        return string.Join('\n', lines);
    }

    private static void ValidateOrdinals(IReadOnlyList<ClassRosterEntry> entries)
    {
        var actual = entries.Select(entry => entry.Ordinal).ToArray();
        var expected = Enumerable.Range(1, entries.Count).ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new PdfStructureException(
                $"La numeración del PDF de clase no es consecutiva: " +
                $"[{string.Join(", ", actual)}].");
        }
    }

    [GeneratedRegex(@"(?im)^GRUPO:\s*(\d+)\s*ESO\s*([A-Z])\b")]
    private static partial Regex GroupHeaderRegex();

    [GeneratedRegex(@"(?i)(\d+)\s*ESO\s*[_ -]*([A-Z])\b")]
    private static partial Regex FileClassRegex();

    [GeneratedRegex(
        @"(?m)^(\d+)\s+(\d{8})\s+(.+?)\s+(\d{1,2})\s+(\d+ESO)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntryRegex();

    [GeneratedRegex("\\r\\n|[\\n\\r\\v\\f\\x1c-\\x1e\\x85\\u2028\\u2029]")]
    private static partial Regex LineBreakRegex();
}

public sealed record ParsedGroupRoster(
    ClassIdentifier ClassIdentifier,
    IReadOnlyList<ClassRosterEntry> Entries);
