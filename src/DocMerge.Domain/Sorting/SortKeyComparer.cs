namespace DocMerge.Domain.Sorting;

public sealed class SortKeyComparer : IComparer<SortKey>
{
    public static readonly SortKeyComparer Instance = new();

    private SortKeyComparer()
    {
    }

    public int Compare(SortKey? left, SortKey? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var first = PythonStringComparer.Instance.Compare(left.First, right.First);
        if (first != 0)
        {
            return first;
        }

        var second = PythonStringComparer.Instance.Compare(left.Second, right.Second);
        return second != 0
            ? second
            : PythonStringComparer.Instance.Compare(left.Third, right.Third);
    }
}
