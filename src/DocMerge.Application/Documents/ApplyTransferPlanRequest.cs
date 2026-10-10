using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Documents;

/// <summary>Describes the files and plan used to produce an unsorted transfer result.</summary>
/// <param name="SourcePath">The source DOCX path.</param>
/// <param name="DestinationPath">The destination DOCX path.</param>
/// <param name="OutputPath">The temporary output path.</param>
/// <param name="Plan">The immutable transfer plan.</param>
/// <param name="Header">The exact identity-column header.</param>
public sealed record ApplyTransferPlanRequest(
    string SourcePath,
    string DestinationPath,
    string OutputPath,
    TransferPlan Plan,
    string Header);
