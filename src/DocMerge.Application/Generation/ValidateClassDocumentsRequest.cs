using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Generation;

/// <summary>Describes validation of a complete temporary generation batch.</summary>
/// <param name="Prepared">The prepared immutable batch.</param>
/// <param name="GeneratedDocuments">The generated temporary documents.</param>
/// <param name="SortingProfile">The expected student sorting profile.</param>
/// <param name="OutputOptions">Optional confirmed document-header values.</param>
public sealed record ValidateClassDocumentsRequest(
    PreparedClassGeneration Prepared,
    IReadOnlyList<GeneratedClassDocument> GeneratedDocuments,
    SortingProfile SortingProfile,
    ClassGenerationOutputOptions? OutputOptions = null);
