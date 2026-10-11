namespace DocMerge.Console.Cli;

public sealed class CliParser
{
    private static readonly HashSet<string> FlagOptions =
    [
        "--simular", "--force", "--help", "-h", "--version", "--generar-clases",
    ];

    private static readonly HashSet<string> ValueOptions =
    [
        "-o", "--output", "--destino", "--lista", "--orden", "--cabecera",
        "--historicos", "--listas", "--plantilla", "--salida",
    ];

    public CliOptions Parse(IReadOnlyList<string> arguments)
    {
        var options = new CliOptions();
        var positionalOnly = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!positionalOnly && argument == "--")
            {
                positionalOnly = true;
                continue;
            }

            if (!positionalOnly && TrySplitValue(argument, out var name, out var value))
            {
                options = ApplyValue(options, name, value);
                continue;
            }

            if (!positionalOnly && FlagOptions.Contains(argument))
            {
                options = ApplyFlag(options, argument);
                continue;
            }

            if (!positionalOnly && ValueOptions.Contains(argument))
            {
                options = ApplyValue(options, argument, ReadValue(arguments, ref index, argument));
                continue;
            }

            options = ApplyPositional(options, argument, positionalOnly);
        }

        return options;
    }

    private static bool TrySplitValue(
        string argument,
        out string name,
        out string value)
    {
        var separator = argument.IndexOf('=');
        if (separator > 0)
        {
            name = argument[..separator];
            value = argument[(separator + 1)..];
            return ValueOptions.Contains(name);
        }

        name = string.Empty;
        value = string.Empty;
        return false;
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        index++;
        if (index >= arguments.Count)
        {
            throw new CliUsageException($"argument {option}: expected one argument");
        }

        return arguments[index];
    }

    private static CliOptions ApplyFlag(CliOptions options, string option) => option switch
    {
        "--simular" => options with { Simulate = true },
        "--force" => options with { Force = true },
        "--help" or "-h" => options with { ShowHelp = true },
        "--version" => options with { ShowVersion = true },
        "--generar-clases" => options with { GenerateClasses = true },
        _ => options,
    };

    private static CliOptions ApplyValue(
        CliOptions options,
        string option,
        string value) => option switch
        {
            "-o" or "--output" => options with { Output = value },
            "--destino" => options with { Destination = value },
            "--lista" => options with { PdfList = value },
            "--orden" => options with { SortRule = value },
            "--cabecera" => options with { Header = value },
            "--historicos" => options with { HistoricalDirectory = value },
            "--listas" => options with { RosterDirectory = value },
            "--plantilla" => options with { TemplatePath = value },
            "--salida" => options with { OutputDirectory = value },
            _ => options,
        };

    private static CliOptions ApplyPositional(
        CliOptions options,
        string argument,
        bool positionalOnly)
    {
        if (!positionalOnly && argument.StartsWith('-'))
        {
            throw new CliUsageException($"unrecognized arguments: {argument}");
        }

        if (options.Source is not null)
        {
            throw new CliUsageException($"unrecognized arguments: {argument}");
        }

        return options with { Source = argument };
    }
}
