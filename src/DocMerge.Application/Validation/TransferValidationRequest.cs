using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Validation;

/// <summary>Describes the result and source material used for transfer validation.</summary>
/// <param name="ResultPath">The generated result path.</param>
/// <param name="SourcePath">The source DOCX path.</param>
/// <param name="DestinationPath">The original destination DOCX path.</param>
/// <param name="Plan">The transfer plan used to generate the result.</param>
/// <param name="SortingProfile">The expected sorting profile.</param>
/// <param name="Header">The exact identity-column header.</param>
public sealed record TransferValidationRequest(
    string ResultPath,
    string SourcePath,
    string DestinationPath,
    TransferPlan Plan,
    SortingProfile SortingProfile,
    string Header);
