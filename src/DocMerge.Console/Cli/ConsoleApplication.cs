using DocMerge.Application.Documents;
using DocMerge.Application.Errors;
using DocMerge.Application.Generation;
using DocMerge.Application.Sorting;
using DocMerge.Application.Transfer;
using DocMerge.Console.Interactive;
using DocMerge.Console.IO;
using DocMerge.Console.Presentation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Console.Cli;

public sealed class ConsoleApplication
{
    private static readonly HashSet<string> AffirmativeAnswers =
        new(StringComparer.Ordinal) { "s", "si", "sí" };

    private readonly CliParser _parser;
    private readonly IConsoleIO _console;
    private readonly InteractiveWizard _wizard;
    private readonly SortDocumentService _sortService;
    private readonly TransferService _transferService;
    private readonly ClassGenerationService? _classGenerationService;
    private readonly ClassGenerationExecutionService? _classGenerationExecutionService;

    public ConsoleApplication(
        CliParser parser,
        IConsoleIO console,
        InteractiveWizard wizard,
        SortDocumentService sortService,
        TransferService transferService,
        ClassGenerationService? classGenerationService = null,
        ClassGenerationExecutionService? classGenerationExecutionService = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _wizard = wizard ?? throw new ArgumentNullException(nameof(wizard));
        _sortService = sortService ?? throw new ArgumentNullException(nameof(sortService));
        _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
        _classGenerationService = classGenerationService;
        _classGenerationExecutionService = classGenerationExecutionService;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var isDoubleClick = arguments.Count == 0;
        var shouldPause = isDoubleClick;
        try
        {
            var options = _parser.Parse(arguments);
            if (ShowInformationalOutput(options))
            {
                return 0;
            }

            if (isDoubleClick)
            {
                options = _wizard.Run(options);
            }

            var context = CliExecutionContextFactory.Create(options);
            await ExecuteContextAsync(context, cancellationToken);
            return 0;
        }
        catch (CliUsageException error)
        {
            return HandleUsageError(error, isDoubleClick);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            shouldPause = false;
            _console.WriteError("Operación cancelada.");
            return isDoubleClick ? 0 : 1;
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
            if (shouldPause)
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

    private async Task ExecuteContextAsync(
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.GenerationRequest is not null)
        {
            await ExecuteClassGenerationAsync(context, cancellationToken);
            return;
        }

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

        var request = new SortDocumentRequest(
            context.SourcePath,
            context.OutputPath,
            SortingProfiles.Get(context.Options.SortRule),
            context.Options.Header);
        var count = await _sortService.ExecuteAsync(request, cancellationToken);
        _console.WriteLine($"Documento creado: {context.OutputPath}");
        _console.WriteLine($"Filas ordenadas: {count}");
    }

    private async Task ExecuteClassGenerationAsync(
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (_classGenerationService is null || _classGenerationExecutionService is null)
        {
            throw new InvalidOperationException(
                "Los servicios de generación de anexos no están configurados.");
        }

        var prepared = await _classGenerationService.PrepareAsync(
            context.GenerationRequest!, cancellationToken);
        if (context.Options.Simulate)
        {
            _console.WriteLine(ClassGenerationPlanFormatter.Format(prepared.Plan, simulation: true));
            return;
        }

        var profile = SortingProfiles.Get(context.Options.SortRule);
        var result = await _classGenerationExecutionService.ExecuteAsync(
            new ClassGenerationExecutionRequest(prepared, context.OutputPath, profile),
            cancellationToken);
        _console.WriteLine($"Lote creado: {result.OutputDirectory}");
        foreach (var document in result.Documents)
        {
            _console.WriteLine($"Documento creado: {document.Path}");
        }

        _console.WriteLine(ClassGenerationPlanFormatter.Format(result.Plan, simulation: false));
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

}
