using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Sorting;

/// <summary>Defines how names are interpreted and converted into sort keys.</summary>
public sealed class SortingProfile
{
    private readonly INameInterpreter _interpreter;
    private readonly Func<NameParts, SortKey> _keyFactory;

    /// <summary>Initializes a new instance of the <see cref="SortingProfile"/> class.</summary>
    /// <param name="identifier">The stable external profile identifier.</param>
    /// <param name="interpreter">The name interpretation strategy.</param>
    /// <param name="keyFactory">The function that orders the interpreted components.</param>
    public SortingProfile(
        string identifier,
        INameInterpreter interpreter,
        Func<NameParts, SortKey> keyFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Identifier = identifier;
        _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        _keyFactory = keyFactory ?? throw new ArgumentNullException(nameof(keyFactory));
    }

    /// <summary>Gets the stable external profile identifier.</summary>
    public string Identifier { get; }

    /// <summary>Creates a normalized sort key for a full name.</summary>
    /// <param name="fullName">The full name to convert.</param>
    /// <returns>The normalized key, or <see cref="SortKey.Invalid"/> for an incomplete name.</returns>
    public SortKey CreateKey(string fullName)
    {
        var parts = _interpreter.Interpret(fullName);
        if (parts is null)
        {
            return SortKey.Invalid;
        }

        var key = _keyFactory(parts);
        return new SortKey(
            PythonUnicode.CaseFold(key.First),
            PythonUnicode.CaseFold(key.Second),
            PythonUnicode.CaseFold(key.Third));
    }
}
