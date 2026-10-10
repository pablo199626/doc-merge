namespace DocMerge.Application.Transfer;

/// <summary>Specifies a stage in a transfer operation.</summary>
public enum OperationStage
{
    /// <summary>Input documents are being read.</summary>
    ReadingInputs,
    /// <summary>The transfer plan is being created.</summary>
    Planning,
    /// <summary>Rows and their dependencies are being imported.</summary>
    ImportingRows,
    /// <summary>The generated rows are being sorted.</summary>
    Sorting,
    /// <summary>The temporary result is being validated.</summary>
    Validating,
    /// <summary>The validated result is being published.</summary>
    Publishing,

    /// <summary>Input hashes are being verified.</summary>
    VerifyingInputs,

    /// <summary>The operation completed successfully.</summary>
    Completed,
}

/// <summary>Reports progress without depending on a presentation technology.</summary>
/// <param name="Stage">The current operation stage.</param>
/// <param name="Completed">The number of completed units.</param>
/// <param name="Total">The total units, when known.</param>
public sealed record OperationProgress(OperationStage Stage, int Completed, int? Total = null);
