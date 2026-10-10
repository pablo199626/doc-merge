using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;

namespace DocMerge.Domain.Transfer;

/// <summary>Represents one valid, immutable action in a transfer plan.</summary>
public sealed record TransferAction
{
    private TransferAction(
        ActionType actionType,
        string identityKey,
        string name,
        IReadOnlyList<PdfEntry> pdfEntries,
        DocumentRecord? source,
        DocumentRecord? destination,
        IssueType? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pdfEntries);
        ActionType = actionType;
        IdentityKey = identityKey;
        Name = name;
        PdfEntries = Array.AsReadOnly(pdfEntries.ToArray());
        Source = source;
        Destination = destination;
        Reason = reason;
    }

    /// <summary>Gets the operation to perform.</summary>
    public ActionType ActionType { get; }

    /// <summary>Gets the normalized identity key.</summary>
    public string IdentityKey { get; }

    /// <summary>Gets the display name associated with the action.</summary>
    public string Name { get; }

    /// <summary>Gets the matching PDF entries.</summary>
    public IReadOnlyList<PdfEntry> PdfEntries { get; }

    /// <summary>Gets the source row when required by the action.</summary>
    public DocumentRecord? Source { get; }

    /// <summary>Gets the destination row when required by the action.</summary>
    public DocumentRecord? Destination { get; }

    /// <summary>Gets the issue that caused a keep or skip action.</summary>
    public IssueType? Reason { get; }

    /// <summary>Creates an action that adds a source row.</summary>
    /// <returns>A validated add action.</returns>
    public static TransferAction Add(
        string identityKey,
        string name,
        IReadOnlyList<PdfEntry> pdfEntries,
        DocumentRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TransferAction(
            ActionType.Add, identityKey, name, pdfEntries, source, null, null);
    }

    /// <summary>Creates an action that replaces a destination row.</summary>
    /// <returns>A validated replace action.</returns>
    public static TransferAction Replace(
        string identityKey,
        string name,
        IReadOnlyList<PdfEntry> pdfEntries,
        DocumentRecord source,
        DocumentRecord destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        return new TransferAction(
            ActionType.Replace, identityKey, name, pdfEntries, source, destination, null);
    }

    /// <summary>Creates an action that preserves a destination row because of an issue.</summary>
    /// <returns>A validated keep action.</returns>
    public static TransferAction Keep(
        string identityKey,
        string name,
        IReadOnlyList<PdfEntry> pdfEntries,
        DocumentRecord destination,
        IssueType reason)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new TransferAction(
            ActionType.Keep, identityKey, name, pdfEntries, null, destination, reason);
    }

    /// <summary>Creates an action that removes a destination row.</summary>
    /// <returns>A validated delete action.</returns>
    public static TransferAction Delete(
        string identityKey,
        string name,
        DocumentRecord destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new TransferAction(
            ActionType.Delete, identityKey, name, [], null, destination, null);
    }

    /// <summary>Creates an action that omits an unsafe transfer.</summary>
    /// <returns>A validated skip action.</returns>
    public static TransferAction Skip(
        string identityKey,
        string name,
        IReadOnlyList<PdfEntry> pdfEntries,
        IssueType reason)
    {
        return new TransferAction(
            ActionType.Skip, identityKey, name, pdfEntries, null, null, reason);
    }
}
