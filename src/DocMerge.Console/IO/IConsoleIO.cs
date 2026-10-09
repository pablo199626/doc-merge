namespace DocMerge.Console.IO;

public interface IConsoleIO
{
    void WriteLine(string value = "");

    void WriteError(string value);

    string ReadLine(string prompt);
}
