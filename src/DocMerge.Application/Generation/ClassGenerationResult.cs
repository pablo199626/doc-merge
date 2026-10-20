using DocMerge.Domain.Generation;

namespace DocMerge.Application.Generation;

/// <summary>Represents a validated and published class-generation batch.</summary>
public sealed record ClassGenerationResult
{
    /// <summary>Initializes a new instance of the <see cref="ClassGenerationResult"/> class.</summary>
    public ClassGenerationResult(
        string outputDirectory,
        IReadOnlyList<GeneratedClassDocument> documents,
        ClassGenerationPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(documents);
        OutputDirectory = outputDirectory;
        Documents = Array.AsReadOnly(documents.ToArray());
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    /// <summary>Gets the published output directory.</summary>
    public string OutputDirectory { get; }

    /// <summary>Gets the published class documents.</summary>
    public IReadOnlyList<GeneratedClassDocument> Documents { get; }

    /// <summary>Gets the plan and structured issues used for generation.</summary>
    public ClassGenerationPlan Plan { get; }
}
