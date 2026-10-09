using System.Reflection;

namespace DocMerge.Console;

public static class ProductVersion
{
    public static string Value { get; } = CreateValue();

    private static string CreateValue()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version
            ?? throw new InvalidOperationException("El ensamblado no tiene versión.");
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
