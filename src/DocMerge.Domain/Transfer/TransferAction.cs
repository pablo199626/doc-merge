using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;

namespace DocMerge.Domain.Transfer;

public sealed record TransferAction(
    ActionType ActionType,
    string IdentityKey,
    string Name,
    IReadOnlyList<PdfEntry> PdfEntries,
    DocumentRecord? Source = null,
    DocumentRecord? Destination = null,
    IssueType? Reason = null);
