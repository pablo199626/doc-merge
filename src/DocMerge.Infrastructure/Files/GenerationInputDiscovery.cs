using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Application.Validation;

namespace DocMerge.Infrastructure.Files;

public sealed class GenerationInputDiscovery : IGenerationInputDiscovery
{
    private const string TemporaryBatchPrefix = ".docmerge_";

    public Task<GenerationInputSet> DiscoverAsync(
        string historicalDirectory,
        string rosterDirectory,
        string templatePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateDirectory(
            historicalDirectory, GenerationInputType.HistoricalDocuments, "históricos");
        ValidateDirectory(rosterDirectory, GenerationInputType.Rosters, "PDF");
        var template = Path.GetFullPath(templatePath);
        var historical = DiscoverFiles(historicalDirectory, ".docx", cancellationToken)
            .Where(path => !string.Equals(path, template, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rosters = DiscoverFiles(rosterDirectory, ".pdf", cancellationToken).ToArray();
        if (historical.Length == 0)
        {
            throw CreateInputException(
                GenerationInputType.HistoricalDocuments,
                historicalDirectory,
                "No se encontraron documentos DOCX históricos.");
        }

        if (rosters.Length == 0)
        {
            throw CreateInputException(
                GenerationInputType.Rosters,
                rosterDirectory,
                "No se encontraron listados PDF de clases.");
        }

        return Task.FromResult(new GenerationInputSet(historical, rosters));
    }

    private static IEnumerable<string> DiscoverFiles(
        string directory,
        string extension,
        CancellationToken cancellationToken)
    {
        var discovered = new List<string>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(directory));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            discovered.AddRange(current.EnumerateFiles()
                .Where(file => string.Equals(file.Extension, extension, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Name.StartsWith("~$", StringComparison.Ordinal))
                .Select(file => file.FullName));
            foreach (var child in current.EnumerateDirectories()
                .Where(CanTraverse)
                .OrderByDescending(item => item.FullName, StringComparer.OrdinalIgnoreCase))
            {
                pending.Push(child);
            }
        }

        foreach (var path in discovered
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return path;
        }
    }

    private static bool CanTraverse(DirectoryInfo directory)
    {
        return !directory.Name.StartsWith(TemporaryBatchPrefix, StringComparison.OrdinalIgnoreCase)
            && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static void ValidateDirectory(
        string path,
        GenerationInputType inputType,
        string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw CreateInputException(
                inputType,
                path,
                $"No existe la carpeta de {description}: {path}");
        }
    }

    private static GenerationInputException CreateInputException(
        GenerationInputType inputType,
        string? path,
        string message)
    {
        var cause = new OperationValidationException(message);
        return new GenerationInputException(inputType, path, message, cause);
    }
}
