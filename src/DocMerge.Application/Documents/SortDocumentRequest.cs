using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Documents;

public sealed record SortDocumentRequest(
    string InputPath,
    string OutputPath,
    SortingProfile SortingProfile,
    string Header);
