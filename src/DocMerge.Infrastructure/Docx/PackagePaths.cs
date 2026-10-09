using DocMerge.Application.Documents;

namespace DocMerge.Infrastructure.Docx;

internal static class PackagePaths
{
    internal static string Resolve(string basePart, string target)
    {
        var combined = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : $"{Directory(basePart)}/{target}";
        return Normalize(combined);
    }

    internal static string RelativeTo(string baseDirectory, string target)
    {
        var baseUri = new Uri($"http://package/{baseDirectory.TrimEnd('/')}/");
        var targetUri = new Uri($"http://package/{target}");
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString());
    }

    internal static string RelationshipsPath(string part)
    {
        return $"{Directory(part)}/_rels/{FileName(part)}.rels".TrimStart('/');
    }

    internal static string Directory(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    private static string FileName(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? path : path[(index + 1)..];
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/'))
        {
            ApplyPart(parts, part);
        }

        return string.Join('/', parts);
    }

    private static void ApplyPart(IList<string> parts, string part)
    {
        if (part.Length == 0 || part == ".")
        {
            return;
        }

        if (part == "..")
        {
            if (parts.Count == 0)
            {
                throw new DocumentStructureException("Una relación apunta fuera del paquete DOCX.");
            }

            parts.RemoveAt(parts.Count - 1);
            return;
        }

        parts.Add(part);
    }
}
