using DocMerge.Application.Generation;
using DocMerge.Application.Transfer;
using DocMerge.Domain.Sorting;

namespace DocMerge.Console.Cli;

internal static class CliExecutionContextFactory
{
    private const string DocxExtension = ".docx";
    private const string PdfExtension = ".pdf";

    internal static ExecutionContext Create(CliOptions options)
    {
        if (options.GenerateClasses)
        {
            return CreateGenerationContext(options);
        }

        RejectGenerationOptions(options);
        if (string.IsNullOrEmpty(options.Source))
        {
            throw new CliUsageException("debes indicar un documento de entrada");
        }

        RequireExistingFile(options.Source, DocxExtension, "el archivo de entrada no es un DOCX existente");
        var transferInputs = BuildTransferInputs(options);
        var output = SelectOutputPath(options, transferInputs);
        if (!string.Equals(Path.GetExtension(output), DocxExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new CliUsageException("el archivo de salida debe tener extensión .docx");
        }

        ValidateDistinctOutput(options.Source, output, transferInputs);
        return new ExecutionContext(options, options.Source, output, transferInputs);
    }

    private static ExecutionContext CreateGenerationContext(CliOptions options)
    {
        if (options.Source is not null
            || options.Destination is not null
            || options.PdfList is not null
            || options.Output is not null)
        {
            throw new CliUsageException(
                "--generar-clases no se combina con source, -o, --destino ni --lista");
        }

        if (options.Force)
        {
            throw new CliUsageException(
                "la generación de anexos publica siempre en una carpeta nueva; no admite --force");
        }

        RequireExistingDirectory(options.HistoricalDirectory, "la carpeta de históricos no existe");
        RequireExistingDirectory(options.RosterDirectory, "la carpeta de listados no existe");
        RequireExistingFile(
            options.TemplatePath,
            DocxExtension,
            "la plantilla no es un DOCX existente");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new CliUsageException("debes indicar --salida para el lote generado");
        }

        var request = new ClassGenerationRequest(
            options.HistoricalDirectory!,
            options.RosterDirectory!,
            options.TemplatePath!,
            options.Header);
        var generationOptions = options.SortRule == SortingProfiles.DefaultIdentifier
            ? options with { SortRule = SortingProfiles.CommaSeparatedIdentifier }
            : options;
        return new ExecutionContext(
            generationOptions, options.TemplatePath!, options.OutputDirectory, null, request);
    }

    private static void RejectGenerationOptions(CliOptions options)
    {
        if (options.HistoricalDirectory is not null
            || options.RosterDirectory is not null
            || options.TemplatePath is not null
            || options.OutputDirectory is not null)
        {
            throw new CliUsageException(
                "--historicos, --listas, --plantilla y --salida requieren --generar-clases");
        }
    }

    private static TransferInputs? BuildTransferInputs(CliOptions options)
    {
        var hasTransferOption = options.Destination is not null || options.PdfList is not null;
        if (!hasTransferOption)
        {
            if (options.Simulate)
            {
                throw new CliUsageException(
                    "--simular solo está disponible junto con --destino y --lista");
            }

            return null;
        }

        if (string.IsNullOrEmpty(options.Destination) || string.IsNullOrEmpty(options.PdfList))
        {
            throw new CliUsageException("--destino y --lista deben indicarse conjuntamente");
        }

        RequireExistingFile(options.Destination, DocxExtension, "el destino no es un DOCX existente");
        RequireExistingFile(options.PdfList, PdfExtension, "el listado no es un PDF existente");
        return new TransferInputs(options.Source!, options.Destination, options.PdfList, options.Header);
    }

    private static string SelectOutputPath(CliOptions options, TransferInputs? transfer)
    {
        if (!string.IsNullOrEmpty(options.Output))
        {
            return options.Output;
        }

        var basis = transfer?.DestinationPath ?? options.Source!;
        var suffix = transfer is null ? "_ordenado" : "_generado";
        return Path.Combine(
            Path.GetDirectoryName(basis) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(basis)}{suffix}{Path.GetExtension(basis)}");
    }

    private static void ValidateDistinctOutput(
        string source,
        string output,
        TransferInputs? transfer)
    {
        var inputs = new List<string> { source };
        if (transfer is not null)
        {
            inputs.Add(transfer.DestinationPath);
            inputs.Add(transfer.ListPath);
        }

        if (inputs.Any(path => string.Equals(
            Path.GetFullPath(path), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)))
        {
            throw new CliUsageException(
                "el archivo de salida debe ser distinto de todos los archivos de entrada");
        }
    }

    private static void RequireExistingFile(string? path, string extension, string description)
    {
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new CliUsageException($"{description}: {path}");
        }
    }

    private static void RequireExistingDirectory(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new CliUsageException($"{description}: {path}");
        }
    }
}
