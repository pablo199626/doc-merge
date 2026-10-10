namespace DocMerge.Domain.Transfer;

/// <summary>Specifies the operation to perform for a planned identity.</summary>
public enum ActionType
{
    /// <summary>Adds a source row to the destination.</summary>
    Add,
    /// <summary>Replaces a destination row with its source row.</summary>
    Replace,
    /// <summary>Preserves an existing destination row.</summary>
    Keep,
    /// <summary>Removes a destination row.</summary>
    Delete,
    /// <summary>Omits an unsafe transfer.</summary>
    Skip,
}
