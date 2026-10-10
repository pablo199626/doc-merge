using System.Collections.ObjectModel;

namespace DocMerge.Domain.Transfer;

/// <summary>Represents an immutable and traceable transfer plan.</summary>
public sealed record TransferPlan
{
    /// <summary>Initializes a new instance of the <see cref="TransferPlan"/> class.</summary>
    public TransferPlan(
        IReadOnlyList<TransferAction> actions,
        IReadOnlyList<TransferIssue> issues,
        string sourceHash,
        string destinationHash,
        string listHash,
        int pdfTotal)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(sourceHash);
        ArgumentNullException.ThrowIfNull(destinationHash);
        ArgumentNullException.ThrowIfNull(listHash);
        ArgumentOutOfRangeException.ThrowIfNegative(pdfTotal);
        Actions = Array.AsReadOnly(actions.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
        SourceHash = sourceHash;
        DestinationHash = destinationHash;
        ListHash = listHash;
        PdfTotal = pdfTotal;
        ActionCounts = CreateActionCounts(Actions);
    }

    /// <summary>Gets the ordered transfer actions.</summary>
    public IReadOnlyList<TransferAction> Actions { get; }

    /// <summary>Gets the ordered issues requiring review.</summary>
    public IReadOnlyList<TransferIssue> Issues { get; }

    /// <summary>Gets the source document hash captured during planning.</summary>
    public string SourceHash { get; }

    /// <summary>Gets the destination document hash captured during planning.</summary>
    public string DestinationHash { get; }

    /// <summary>Gets the PDF hash captured during planning.</summary>
    public string ListHash { get; }

    /// <summary>Gets the total declared by the PDF.</summary>
    public int PdfTotal { get; }

    /// <summary>Gets the number of actions for each action type.</summary>
    public IReadOnlyDictionary<ActionType, int> ActionCounts { get; }

    private static IReadOnlyDictionary<ActionType, int> CreateActionCounts(
        IReadOnlyList<TransferAction> actions)
    {
        var counts = Enum.GetValues<ActionType>()
            .ToDictionary(type => type, type => actions.Count(action => action.ActionType == type));
        return new ReadOnlyDictionary<ActionType, int>(counts);
    }
}
