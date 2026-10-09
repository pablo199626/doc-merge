using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Validation;

public sealed record TransferValidationRequest(
    string ResultPath,
    string DestinationPath,
    TransferPlan Plan,
    SortingProfile SortingProfile,
    string Header);
