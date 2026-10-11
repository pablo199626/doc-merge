using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Generation;

/// <summary>Describes the temporary generation of every document in a prepared batch.</summary>
/// <param name="Prepared">The prepared class-generation batch.</param>
/// <param name="WorkingDirectory">The private temporary output directory.</param>
/// <param name="SortingProfile">The profile used to order generated student rows.</param>
public sealed record GenerateClassDocumentsRequest(
    PreparedClassGeneration Prepared,
    string WorkingDirectory,
    SortingProfile SortingProfile);
