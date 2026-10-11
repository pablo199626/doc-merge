using DocMerge.Application.Files;
using DocMerge.Application.Validation;

namespace DocMerge.Infrastructure.Files;

public sealed class GenerationInputDiscovery : IGenerationInputDiscovery
{
    public GenerationInputSet Discover(
        string historicalDirectory,
        string rosterDirectory,
        string templatePath)
    {
        ValidateDirectory(historicalDirectory, "históricos");
        ValidateDirectory(rosterDirectory, "PDF");
        var template = Path.GetFullPath(templatePath);
        var historical = DiscoverFiles(historicalDirectory, ".docx")
            .Where(path => !string.Equals(path, template, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rosters = DiscoverFiles(rosterDirectory, ".pdf").ToArray();
        if (historical.Length == 0)
        {
            throw new OperationValidationException(
                "No se encontraron documentos DOCX históricos.");
        }

        if (rosters.Length == 0)
        {
            throw new OperationValidationException("No se encontraron listados PDF de clases.");
        }

        return new GenerationInputSet(historical, rosters);
    }

    private static IEnumerable<string> DiscoverFiles(string directory, string extension)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            MatchCasing = MatchCasing.CaseInsensitive,
        };
        return Directory.EnumerateFiles(directory, $"*{extension}", options)
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateDirectory(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new OperationValidationException(
                $"No existe la carpeta de {description}: {path}");
        }
    }
}
