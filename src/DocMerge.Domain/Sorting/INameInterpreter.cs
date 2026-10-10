namespace DocMerge.Domain.Sorting;

/// <summary>Interprets a full name into components used by sorting profiles.</summary>
public interface INameInterpreter
{
    /// <summary>Interprets a full name.</summary>
    /// <param name="fullName">The full name to interpret.</param>
    /// <returns>The interpreted components, or <see langword="null"/> when the name is incomplete.</returns>
    NameParts? Interpret(string fullName);
}
