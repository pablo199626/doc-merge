using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Documents;

/// <summary>Describes a document sorting operation.</summary>
/// <param name="InputPath">The input DOCX path.</param>
/// <param name="OutputPath">The output DOCX path.</param>
/// <param name="SortingProfile">The sorting profile.</param>
/// <param name="Header">The exact identity-column header.</param>
public sealed record SortDocumentRequest(
    string InputPath,
    string OutputPath,
    SortingProfile SortingProfile,
    string Header);
