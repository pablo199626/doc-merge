namespace DocMerge.Domain.Sorting;

/// <summary>Represents the three components used to order a name.</summary>
/// <param name="First">The primary sort component.</param>
/// <param name="Second">The secondary sort component.</param>
/// <param name="Third">The tertiary sort component.</param>
public sealed record SortKey(string First, string Second, string Third)
{
    /// <summary>Gets a key that sorts after every valid interpreted name.</summary>
    public static readonly SortKey Invalid = new("\uffff", "\uffff", "\uffff");
}
