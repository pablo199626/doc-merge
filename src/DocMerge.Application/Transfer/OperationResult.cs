using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

public sealed record OperationResult(string OutputPath, TransferPlan Plan);
