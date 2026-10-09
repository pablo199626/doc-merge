namespace DocMerge.Console.IO;

public sealed class SystemConsoleIO : IConsoleIO
{
    public void WriteLine(string value = "")
    {
        System.Console.WriteLine(value);
    }

    public void WriteError(string value)
    {
        System.Console.Error.WriteLine(value);
    }

    public string ReadLine(string prompt)
    {
        System.Console.Write(prompt);
        return System.Console.ReadLine() ?? string.Empty;
    }
}
