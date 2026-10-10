using DocMerge.Console.Cli;
using DocMerge.Console.IO;

namespace DocMerge.Console.Interactive;

public sealed class InteractiveWizard
{
    private static readonly HashSet<string> AffirmativeAnswers =
        new(StringComparer.Ordinal) { "s", "si", "sí" };

    private readonly IConsoleIO _console;

    public InteractiveWizard(IConsoleIO console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public CliOptions Run(CliOptions options)
    {
        ShowHeader();
        var selected = _console.ReadLine(Environment.NewLine + "Elige una opción [1]: ").Trim();
        selected = selected.Length == 0 ? "1" : selected;
        if (selected is not ("1" or "2"))
        {
            throw new CliUsageException("la opción debe ser 1 o 2");
        }

        var source = CleanPath(_console.ReadLine("Ruta del documento Word de origen: "));
        return selected == "2"
            ? CreateSortOptions(options, source)
            : CreateTransferOptions(options, source);
    }

    private CliOptions CreateSortOptions(CliOptions options, string source)
    {
        return options with
        {
            Source = source,
            Output = AskOptionalOutput(),
            IsInteractive = true,
        };
    }

    private CliOptions CreateTransferOptions(CliOptions options, string source)
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

    private void ShowHeader()
    {
        _console.WriteLine($"DOCMERGE {ProductVersion.Value}");
        _console.WriteLine("==============");
        _console.WriteLine("1. Transferir personas usando un listado PDF");
        _console.WriteLine("2. Ordenar alfabéticamente un documento Word");
    }

    private static string CleanPath(string value)
    {
        return value.Trim().Trim('"').Trim('\'');
    }
}
