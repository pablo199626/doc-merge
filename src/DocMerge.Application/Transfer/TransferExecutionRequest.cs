using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

/// <summary>Describes a complete transfer execution.</summary>
/// <param name="Inputs">The transfer inputs.</param>
/// <param name="OutputPath">The final output path.</param>
/// <param name="SortingProfile">The profile used to sort the result.</param>
/// <param name="Plan">An optional previously prepared plan.</param>
public sealed record TransferExecutionRequest(
    TransferInputs Inputs,
    string OutputPath,
    SortingProfile SortingProfile,
    TransferPlan? Plan = null);
