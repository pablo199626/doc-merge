using System.Reflection;

namespace DocMerge.Console;

/// <summary>Provides the product version embedded in the entry assembly.</summary>
public static class ProductVersion
{
    /// <summary>Gets the public semantic product version.</summary>
    public static string Value { get; } = CreateValue();

    private static string CreateValue()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version
            ?? throw new InvalidOperationException("El ensamblado no tiene versión.");
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
