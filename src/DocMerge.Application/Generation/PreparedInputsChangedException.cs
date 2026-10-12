using DocMerge.Application.Errors;

namespace DocMerge.Application.Generation;

/// <summary>Indicates that reviewed generation inputs no longer match the prepared snapshot.</summary>
public sealed class PreparedInputsChangedException : DocMergeException
{
    /// <summary>Initializes a stale-review failure.</summary>
    public PreparedInputsChangedException(string message)
        : base(message)
    {
    }
}
