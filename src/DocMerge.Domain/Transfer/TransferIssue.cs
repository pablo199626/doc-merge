namespace DocMerge.Domain.Transfer;

/// <summary>Describes a transfer condition that requires review.</summary>
public sealed record TransferIssue
{
    /// <summary>Initializes a new instance of the <see cref="TransferIssue"/> class.</summary>
    public TransferIssue(
        IssueType issueType,
        string identityKey,
        string name,
        string detail,
        IReadOnlyList<string> references)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentNullException.ThrowIfNull(references);
        IssueType = issueType;
        IdentityKey = identityKey;
        Name = name;
        Detail = detail;
        References = Array.AsReadOnly(references.ToArray());
    }

    /// <summary>Gets the issue classification.</summary>
    public IssueType IssueType { get; }

    /// <summary>Gets the normalized identity key.</summary>
    public string IdentityKey { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the user-facing issue detail.</summary>
    public string Detail { get; }

    /// <summary>Gets the source locations relevant to the issue.</summary>
    public IReadOnlyList<string> References { get; }
}
