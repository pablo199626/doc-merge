using System.Text;

namespace DocMerge.Domain.Sorting;

/// <summary>Compares strings by Unicode code point to match Python ordering.</summary>
public sealed class PythonStringComparer : IComparer<string>
{
    /// <summary>Gets the shared comparer instance.</summary>
    public static readonly PythonStringComparer Instance = new();

    private PythonStringComparer()
    {
    }

    /// <inheritdoc/>
    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var leftRunes = left.EnumerateRunes().GetEnumerator();
        var rightRunes = right.EnumerateRunes().GetEnumerator();
        return CompareRunes(ref leftRunes, ref rightRunes);
    }

    private static int CompareRunes(
        ref StringRuneEnumerator left,
        ref StringRuneEnumerator right)
    {
        while (left.MoveNext())
        {
            if (!right.MoveNext())
            {
                return 1;
            }

            var comparison = left.Current.Value.CompareTo(right.Current.Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return right.MoveNext() ? -1 : 0;
    }
}
