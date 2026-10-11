namespace DocMerge.Domain.Generation;

/// <summary>Identifies whether a generation issue permits final publication.</summary>
public enum GenerationIssueSeverity
{
    /// <summary>The issue is reported but permits generation.</summary>
    Warning,

    /// <summary>The issue prevents final publication.</summary>
    Blocking,
}
