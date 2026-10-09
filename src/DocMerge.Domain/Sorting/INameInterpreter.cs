namespace DocMerge.Domain.Sorting;

public interface INameInterpreter
{
    NameParts? Interpret(string fullName);
}
