using DocMerge.Domain.Identity;

namespace DocMerge.Domain.Sorting;

public sealed class SortingProfile
{
    private readonly INameInterpreter _interpreter;
    private readonly Func<NameParts, SortKey> _keyFactory;

    public SortingProfile(
        string identifier,
        INameInterpreter interpreter,
        Func<NameParts, SortKey> keyFactory)
    {
        Identifier = identifier;
        _interpreter = interpreter;
        _keyFactory = keyFactory;
    }

    public string Identifier { get; }

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
