using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DocMerge.Domain.Identity;

internal static class UnicodeData
{
    private const string CaseFoldingResource = "CaseFolding-15.0.txt";
    private const string WhitespaceResource = "Whitespace-15.0.txt";

    internal static readonly FrozenDictionary<int, string> CaseFolding =
        LoadCaseFolding();

    internal static readonly FrozenSet<int> Whitespace = LoadWhitespace();

    private static FrozenDictionary<int, string> LoadCaseFolding()
    {
        return ReadLines(CaseFoldingResource)
            .Select(ParseCaseFolding)
            .ToFrozenDictionary(mapping => mapping.CodePoint, mapping => mapping.Value);
    }

    private static FrozenSet<int> LoadWhitespace()
    {
        return ReadLines(WhitespaceResource)
            .Select(line => int.Parse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToFrozenSet();
    }

    private static (int CodePoint, string Value) ParseCaseFolding(string line)
    {
        var fields = line.Split(';');
        var codePoint = int.Parse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var value = string.Concat(fields[1].Split(' ').Select(ParseCodePoint));
        return (codePoint, value);
    }

    private static string ParseCodePoint(string value)
    {
        var codePoint = int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return char.ConvertFromUtf32(codePoint);
    }

    private static IEnumerable<string> ReadLines(string suffix)
    {
        var assembly = typeof(UnicodeData).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el recurso Unicode {suffix}.");
        using var reader = new StreamReader(stream, Encoding.ASCII);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
