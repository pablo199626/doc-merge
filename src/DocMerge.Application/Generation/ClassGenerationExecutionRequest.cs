using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Generation;

/// <summary>Identifies a reviewed batch and the new directory where it must be published.</summary>
/// <param name="Prepared">The exact prepared batch reviewed by the user.</param>
/// <param name="OutputDirectory">The new output directory.</param>
/// <param name="SortingProfile">The output sorting profile.</param>
/// <param name="OutputOptions">Optional confirmed document-header values.</param>
public sealed record ClassGenerationExecutionRequest(
    PreparedClassGeneration Prepared,
    string OutputDirectory,
    SortingProfile SortingProfile,
    ClassGenerationOutputOptions? OutputOptions = null);
