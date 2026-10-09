using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Documents;

public sealed record ApplyTransferPlanRequest(
    string SourcePath,
    string DestinationPath,
    string OutputPath,
    TransferPlan Plan,
    string Header);
