using DocMerge.Application.Transfer;

namespace DocMerge.Console.Cli;

internal static class CliExecutionContextFactory
{
    private const string DocxExtension = ".docx";
    private const string PdfExtension = ".pdf";

    internal static ExecutionContext Create(CliOptions options)
    {
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

    private static void RequireExistingFile(string path, string extension, string description)
    {
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new CliUsageException($"{description}: {path}");
        }
    }
}
