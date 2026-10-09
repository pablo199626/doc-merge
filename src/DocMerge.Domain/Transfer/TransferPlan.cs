namespace DocMerge.Domain.Transfer;

public sealed record TransferPlan(
    IReadOnlyList<TransferAction> Actions,
    IReadOnlyList<TransferIssue> Issues,
    string SourceHash,
    string DestinationHash,
    string ListHash,
    int PdfTotal)
{
    public IReadOnlyDictionary<ActionType, int> ActionCounts => Enum
        .GetValues<ActionType>()
        .ToDictionary(type => type, type => Actions.Count(action => action.ActionType == type));
}
