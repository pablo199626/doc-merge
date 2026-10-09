namespace DocMerge.Domain.Transfer;

public sealed record TransferIssue(
    IssueType IssueType,
    string IdentityKey,
    string Name,
    string Detail,
    IReadOnlyList<string> References);
