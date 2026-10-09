using DocMerge.Application.Errors;
using DocMerge.Application.Sorting;
using DocMerge.Application.Transfer;
using DocMerge.Console.IO;
using DocMerge.Console.Presentation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Console.Cli;

public sealed class ConsoleApplication
{
    private const string DocxExtension = ".docx";
    private const string PdfExtension = ".pdf";
    private static readonly HashSet<string> AffirmativeAnswers =
        new(StringComparer.Ordinal) { "s", "si", "sí" };

    private readonly CliParser _parser;
    private readonly IConsoleIO _console;
    private readonly SortDocumentService _sortService;
    private readonly TransferService _transferService;

    public ConsoleApplication(
        CliParser parser,
        IConsoleIO console,
        SortDocumentService sortService,
        TransferService transferService)
    {
        _parser = parser;
        _console = console;
        _sortService = sortService;
        _transferService = transferService;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var isDoubleClick = arguments.Count == 0;
        try
        {
            var options = _parser.Parse(arguments);
            if (ShowInformationalOutput(options))
            {
                return 0;
            }

            if (isDoubleClick)
            {
                options = RunWizard(options);
            }

            var context = BuildContext(options);
            await ExecuteContextAsync(context, cancellationToken);
            return 0;
        }
        catch (CliUsageException error)
        {
            return HandleUsageError(error, isDoubleClick);
        }
        catch (Exception error) when (error is IOException
            or InvalidDataException
            or ArgumentException
            or DocMergeException)
        {
            _console.WriteError($"Error: {error.Message}");
            return isDoubleClick ? 0 : 1;
        }
        finally
        {
            if (isDoubleClick)
            {
                _console.ReadLine(Environment.NewLine + "Pulsa Intro para cerrar...");
            }
        }
    }

    private bool ShowInformationalOutput(CliOptions options)
    {
        if (options.ShowHelp)
        {
            _console.WriteLine(ConsoleText.Help);
            return true;
        }

        if (options.ShowVersion)
        {
            _console.WriteLine($"docmerge {ProductVersion.Value}");
            return true;
        }

        return false;
    }

    private CliOptions RunWizard(CliOptions options)
    {
        ShowWizardHeader();
        var selected = _console.ReadLine(Environment.NewLine + "Elige una opción [1]: ").Trim();
        selected = selected.Length == 0 ? "1" : selected;
        if (selected is not ("1" or "2"))
        {
            throw new CliUsageException("la opción debe ser 1 o 2");
        }

        var source = CleanPath(_console.ReadLine("Ruta del documento Word de origen: "));
        return selected == "2"
            ? SortWizardOptions(options, source)
            : TransferWizardOptions(options, source);
    }

    private CliOptions SortWizardOptions(CliOptions options, string source)
    {
        return options with
        {
            Source = source,
            Output = AskOptionalOutput(),
            IsInteractive = true,
        };
    }

    private CliOptions TransferWizardOptions(CliOptions options, string source)
    {
        var destination = CleanPath(_console.ReadLine("Ruta del documento Word de destino: "));
        var pdfList = CleanPath(_console.ReadLine("Ruta del listado PDF: "));
        var output = AskOptionalOutput();
        var answer = _console.ReadLine(
            "¿Solo simular sin crear el documento? (s/n) [n]: ").Trim();
        return options with
        {
            Source = source,
            Destination = destination,
            PdfList = pdfList,
            Output = output,
            Simulate = AffirmativeAnswers.Contains(answer.ToLowerInvariant()),
            IsInteractive = true,
        };
    }

    private string? AskOptionalOutput()
    {
        var output = CleanPath(_console.ReadLine(
            "Ruta de salida (Intro para usar la predeterminada): "));
        return output.Length == 0 ? null : output;
    }

    private void ShowWizardHeader()
    {
        _console.WriteLine($"DOCMERGE {ProductVersion.Value}");
        _console.WriteLine("==============");
        _console.WriteLine("1. Transferir personas usando un listado PDF");
        _console.WriteLine("2. Ordenar alfabéticamente un documento Word");
    }

    private static ExecutionContext BuildContext(CliOptions options)
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

    private async Task ExecuteContextAsync(
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!ConfirmOverwrite(context))
        {
            _console.WriteLine("Operación cancelada.");
            return;
        }

        if (context.TransferInputs is not null)
        {
            await ExecuteTransferAsync(context, cancellationToken);
            return;
        }

        var count = await _sortService.ExecuteAsync(
            context.SourcePath,
            context.OutputPath,
            SortingProfiles.Get(context.Options.SortRule),
            context.Options.Header,
            cancellationToken);
        _console.WriteLine($"Documento creado: {context.OutputPath}");
        _console.WriteLine($"Filas ordenadas: {count}");
    }

    private async Task ExecuteTransferAsync(
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        var plan = await _transferService.PrepareAsync(context.TransferInputs!, cancellationToken);
        if (context.Options.Simulate)
        {
            _console.WriteLine(TransferPlanFormatter.FormatSimulation(plan));
            _console.WriteLine(TransferPlanFormatter.FormatIssues(plan));
            return;
        }

        var request = new TransferExecutionRequest(
            context.TransferInputs!,
            context.OutputPath,
            SortingProfiles.Get(context.Options.SortRule),
            plan);
        await _transferService.ExecuteAsync(request, null, cancellationToken);
        _console.WriteLine($"Documento creado: {context.OutputPath}");
        _console.WriteLine(TransferPlanFormatter.FormatExecution(plan));
        _console.WriteLine(TransferPlanFormatter.FormatIssues(plan));
    }

    private bool ConfirmOverwrite(ExecutionContext context)
    {
        if (context.Options.Simulate || !File.Exists(context.OutputPath) || context.Options.Force)
        {
            return true;
        }

        if (!context.Options.IsInteractive)
        {
            throw new CliUsageException(
                $"ya existe el archivo de salida: {context.OutputPath}. Usa --force para sobrescribirlo");
        }

        var answer = _console.ReadLine(
            $"{context.OutputPath} ya existe. ¿Sobrescribir? (s/n): ").Trim();
        return AffirmativeAnswers.Contains(answer.ToLowerInvariant());
    }

    private int HandleUsageError(CliUsageException error, bool isDoubleClick)
    {
        if (isDoubleClick)
        {
            _console.WriteError($"Error: {error.Message}");
            return 0;
        }

        _console.WriteError(ConsoleText.Usage);
        _console.WriteError($"docmerge: error: {error.Message}");
        return 2;
    }

    private static void RequireExistingFile(string path, string extension, string description)
    {
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new CliUsageException($"{description}: {path}");
        }
    }

    private static string CleanPath(string value)
    {
        return value.Trim().Trim('"').Trim('\'');
    }
}
