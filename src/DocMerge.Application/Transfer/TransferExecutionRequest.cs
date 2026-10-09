using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

public sealed record TransferExecutionRequest(
    TransferInputs Inputs,
    string OutputPath,
    SortingProfile SortingProfile,
    TransferPlan? Plan = null);
