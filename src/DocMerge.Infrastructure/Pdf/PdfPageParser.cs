using System.Globalization;
using System.Text.RegularExpressions;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Identity;
using DocMerge.Domain.Pdf;

namespace DocMerge.Infrastructure.Pdf;

public sealed partial class PdfPageParser
{
    private const string ListTitle = "lista de nombres";

    public PdfReadResult Parse(IReadOnlyList<string> pageTexts)
    {
        if (pageTexts.Count == 0 || pageTexts.All(string.IsNullOrWhiteSpace))
        {
            throw new PdfTextMissingException(
                "El PDF no contiene texto extraíble; puede necesitar OCR.");
        }

        var pages = pageTexts.Select(NormalizeLines).ToArray();
        var declaredTotal = ExtractDeclaredTotal(pages);
        var candidates = ExtractCandidates(pages);
        ValidateCandidates(candidates, declaredTotal);
        var entries = candidates.Select(CreateEntry).ToArray();
        return new PdfReadResult(entries, declaredTotal, pageTexts.Count);
    }

    private static IReadOnlyList<Candidate> ExtractCandidates(IReadOnlyList<string[]> pages)
    {
        var candidates = new List<Candidate>();
        var hasStarted = false;
        for (var index = 0; index < pages.Count; index++)
        {
            var result = ExtractPageCandidates(pages[index], index + 1, hasStarted);
            candidates.AddRange(result.Candidates);
            hasStarted = result.HasStarted;
        }

        return candidates;
    }

    private static PageCandidates ExtractPageCandidates(
        IReadOnlyList<string> lines,
        int pageNumber,
        bool hasStarted)
    {
        var start = FindContentStart(lines, pageNumber, hasStarted);
        var ordinals = FindOrdinals(lines, start.Index);
        var candidates = ordinals.Select((ordinal, position) =>
            CreateCandidate(lines, pageNumber, ordinals, position, ordinal)).ToArray();
        return new PageCandidates(candidates, start.HasStarted);
    }

    private static ContentStart FindContentStart(
        IReadOnlyList<string> lines,
        int pageNumber,
        bool hasStarted)
    {
        var sections = FindMatches(lines, SectionRegex());
        var headers = FindMatches(lines, HeaderRegex());
        if (sections.Count > 0)
        {
            return FindHeaderAfterSection(sections[^1], headers, pageNumber);
        }

        if (hasStarted && headers.Count > 0)
        {
            return new ContentStart(headers[^1] + 1, true);
        }

        return new ContentStart(hasStarted ? 0 : lines.Count, hasStarted);
    }

    private static ContentStart FindHeaderAfterSection(
        int sectionIndex,
        IReadOnlyList<int> headers,
        int pageNumber)
    {
        var following = headers.Where(index => index > sectionIndex).ToArray();
        if (following.Length == 0)
        {
            throw new PdfStructureException(
                $"La sección Alumnado de la página {pageNumber} no tiene la cabecera Nombre y apellidos.");
        }

        return new ContentStart(following[^1] + 1, true);
    }

    private static Candidate CreateCandidate(
        IReadOnlyList<string> lines,
        int pageNumber,
        IReadOnlyList<OrdinalPosition> ordinals,
        int position,
        OrdinalPosition ordinal)
    {
        var end = position + 1 < ordinals.Count ? ordinals[position + 1].Index : lines.Count;
        var parts = lines.Skip(ordinal.Index + 1).Take(end - ordinal.Index - 1)
            .Where(line => !IsControlLine(line)).ToArray();
        if (parts.Length == 0)
        {
            throw new PdfStructureException(
                $"La entrada {ordinal.Ordinal} de la página {pageNumber} no contiene un nombre.");
        }

        return new Candidate(ordinal.Ordinal, string.Join(' ', parts), pageNumber);
    }

    private static IReadOnlyList<OrdinalPosition> FindOrdinals(
        IReadOnlyList<string> lines,
        int start)
    {
        return lines.Select((line, index) => (line, index))
            .Skip(start)
            .Select(item => (item.index, Match: OrdinalRegex().Match(item.line)))
            .Where(item => item.Match.Success)
            .Select(item => new OrdinalPosition(
                item.index,
                int.Parse(item.Match.Groups[1].Value, CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static int ExtractDeclaredTotal(IReadOnlyList<string[]> pages)
    {
        var totals = pages.SelectMany(lines => lines)
            .Select(line => SectionRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
        ValidateDeclaredTotals(totals);
        return totals[0];
    }

    private static void ValidateDeclaredTotals(IReadOnlyList<int> totals)
    {
        if (totals.Count == 0)
        {
            throw new PdfStructureException("No se encontró una sección \"Alumnado (n)\".");
        }

        if (totals.Distinct().Count() != 1)
        {
            throw new PdfStructureException("Las cabeceras Alumnado declaran totales diferentes.");
        }

        if (totals[0] <= 0)
        {
            throw new PdfStructureException(
                "La lista de alumnado está vacía; no se autoriza el procesamiento.");
        }
    }

    private static void ValidateCandidates(
        IReadOnlyList<Candidate> candidates,
        int declaredTotal)
    {
        if (candidates.Count != declaredTotal)
        {
            throw new PdfStructureException(
                $"La lista declara {declaredTotal} personas, pero se extrajeron {candidates.Count}.");
        }

        var actual = candidates.Select(candidate => candidate.Ordinal).ToArray();
        var expected = Enumerable.Range(1, declaredTotal).ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new PdfStructureException(
                $"La numeración extraída no es consecutiva: [{string.Join(", ", actual)}]; " +
                $"se esperaba [{string.Join(", ", expected)}].");
        }
    }

    private static string[] NormalizeLines(string text)
    {
        return SplitLines(text)
            .Select(IdentityNormalizer.NormalizeWhitespace)
            .ToArray();
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        return Regex.Split(value, "\\r\\n|[\\n\\r\\v\\f\\x1c-\\x1e\\x85\\u2028\\u2029]");
    }

    private static bool IsControlLine(string line)
    {
        if (line.Length == 0)
        {
            return true;
        }

        return SectionRegex().IsMatch(line)
            || HeaderRegex().IsMatch(line)
            || NumberRegex().IsMatch(line)
            || IdentityNormalizer.Normalize(line) == ListTitle;
    }

    private static List<int> FindMatches(IReadOnlyList<string> lines, Regex regex)
    {
        return lines.Select((line, index) => (line, index))
            .Where(item => regex.IsMatch(item.line))
            .Select(item => item.index)
            .ToList();
    }

    private static PdfEntry CreateEntry(Candidate candidate)
    {
        return new PdfEntry(
            candidate.Name,
            IdentityNormalizer.Normalize(candidate.Name),
            candidate.PageNumber,
            candidate.Ordinal);
    }

    [GeneratedRegex(@"^Alumnado\s*\(\s*(\d+)\s*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^Nombre\s+y\s+apellidos\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^(\d+)\s*[.)º°]?$", RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalRegex();

    [GeneratedRegex(@"^N[.º°oª]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    private sealed record Candidate(int Ordinal, string Name, int PageNumber);

    private sealed record OrdinalPosition(int Index, int Ordinal);

    private sealed record ContentStart(int Index, bool HasStarted);

    private sealed record PageCandidates(IReadOnlyList<Candidate> Candidates, bool HasStarted);
}
