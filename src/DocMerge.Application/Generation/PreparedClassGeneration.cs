using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Domain.Generation;

namespace DocMerge.Application.Generation;

/// <summary>Contains all immutable data prepared before class-document generation.</summary>
public sealed record PreparedClassGeneration
{
    /// <summary>Initializes a new instance of the <see cref="PreparedClassGeneration"/> class.</summary>
    public PreparedClassGeneration(
        ClassGenerationRequest request,
        GenerationInputSet inputs,
        GenerationTemplateSnapshot template,
        IReadOnlyList<HistoricalDocumentSnapshot> historicalDocuments,
        ClassGenerationPlan plan)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        ArgumentNullException.ThrowIfNull(historicalDocuments);
        HistoricalDocuments = Array.AsReadOnly(historicalDocuments.ToArray());
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    /// <summary>Gets the original request.</summary>
    public ClassGenerationRequest Request { get; }

    /// <summary>Gets the deterministic discovered inputs.</summary>
    public GenerationInputSet Inputs { get; }

    /// <summary>Gets the validated template snapshot.</summary>
    public GenerationTemplateSnapshot Template { get; }

    /// <summary>Gets the prepared historical documents.</summary>
    public IReadOnlyList<HistoricalDocumentSnapshot> HistoricalDocuments { get; }

    /// <summary>Gets the immutable generation plan.</summary>
    public ClassGenerationPlan Plan { get; }
}
