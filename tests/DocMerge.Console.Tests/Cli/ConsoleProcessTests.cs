using System.Diagnostics;
using DocMerge.Console;

namespace DocMerge.Console.Tests.Cli;

public sealed class ConsoleProcessTests
{
    [Fact]
    public async Task Main_VersionOption_PrintsSynchronizedVersion()
    {
        var result = await RunAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("docmerge 2.1.0", result.Output.Trim());
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task Main_InvalidOption_PrintsUsageAndReturnsTwo()
    {
        var result = await RunAsync("--unknown");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("usage: docmerge", result.Error, StringComparison.Ordinal);
        Assert.Contains("unrecognized arguments: --unknown", result.Error, StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var root = FindRepositoryRoot();
        var assembly = Path.Combine(
            root, "src", "DocMerge.Console", "bin", "Debug", "net10.0", "win-x64",
            "DocMerge.dll");
        var start = new ProcessStartInfo("C:\\Program Files\\dotnet\\dotnet.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("No se pudo iniciar docmerge.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocMerge.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
