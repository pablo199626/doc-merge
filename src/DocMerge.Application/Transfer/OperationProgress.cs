namespace DocMerge.Application.Transfer;

public enum OperationStage
{
    ReadingInputs,
    Planning,
    ImportingRows,
    Sorting,
    Validating,
    Publishing,
}

public sealed record OperationProgress(OperationStage Stage, int Completed, int? Total = null);
