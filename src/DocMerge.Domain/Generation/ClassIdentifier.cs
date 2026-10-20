namespace DocMerge.Domain.Generation;

/// <summary>Identifies one generated class by educational stage, level, and group.</summary>
public sealed record ClassIdentifier
{
    /// <summary>Initializes a new instance of the <see cref="ClassIdentifier"/> class.</summary>
    /// <param name="stage">The normalized educational stage.</param>
    /// <param name="level">The positive level number.</param>
    /// <param name="group">The normalized group designator.</param>
    public ClassIdentifier(string stage, int level, string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        Stage = stage.ToUpperInvariant();
        Level = level;
        Group = group.ToUpperInvariant();
    }

    /// <summary>Gets the normalized educational stage.</summary>
    public string Stage { get; }

    /// <summary>Gets the positive level number.</summary>
    public int Level { get; }

    /// <summary>Gets the normalized group designator.</summary>
    public string Group { get; }

    /// <summary>Gets the canonical class code used for cross-source validation.</summary>
    public string CanonicalCode => $"{Level}{Stage}{Group}";

    /// <summary>Gets the unambiguous default output stem.</summary>
    public string OutputStem => $"{Level}{Group}";
}
