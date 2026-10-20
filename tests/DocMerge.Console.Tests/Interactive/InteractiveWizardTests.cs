using DocMerge.Console.Cli;
using DocMerge.Console.Interactive;
using DocMerge.Console.IO;

namespace DocMerge.Console.Tests.Interactive;

public sealed class InteractiveWizardTests
{
    [Fact]
    public void Run_ClassGeneration_CapturesFolderTemplateAndOutputPaths()
    {
        var console = new FakeConsole(
            ["3", "\"C:\\historical\"", "C:\\pdf", "C:\\template.docx", "C:\\output", "s"]);
        var wizard = new InteractiveWizard(console);

        var result = wizard.Run(new CliOptions());

        Assert.True(result.GenerateClasses);
        Assert.Equal(@"C:\historical", result.HistoricalDirectory);
        Assert.Equal(@"C:\pdf", result.RosterDirectory);
        Assert.Equal(@"C:\template.docx", result.TemplatePath);
        Assert.Equal(@"C:\output", result.OutputDirectory);
        Assert.True(result.Simulate);
        Assert.True(result.IsInteractive);
    }

    private sealed class FakeConsole : IConsoleIO
    {
        private readonly Queue<string> _answers;

        internal FakeConsole(IEnumerable<string> answers)
        {
            _answers = new Queue<string>(answers);
        }

        public void WriteLine(string value = "")
        {
        }

        public void WriteError(string value)
        {
        }

        public string ReadLine(string prompt) => _answers.Dequeue();
    }
}
