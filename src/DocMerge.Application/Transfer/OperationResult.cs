using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

/// <summary>Represents a successfully published transfer result.</summary>
/// <param name="OutputPath">The published output path.</param>
/// <param name="Plan">The plan used to create the output.</param>
public sealed record OperationResult(string OutputPath, TransferPlan Plan);
