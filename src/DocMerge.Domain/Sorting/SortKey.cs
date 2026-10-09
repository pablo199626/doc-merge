namespace DocMerge.Domain.Sorting;

public sealed record SortKey(string First, string Second, string Third)
{
    public static readonly SortKey Invalid = new("\uffff", "\uffff", "\uffff");
}
