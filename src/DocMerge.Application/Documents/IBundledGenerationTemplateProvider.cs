namespace DocMerge.Application.Documents;

/// <summary>Provides a verified working copy of the official embedded template.</summary>
public interface IBundledGenerationTemplateProvider
{
    /// <summary>Materializes and verifies the official template for this application session.</summary>
    Task<BundledGenerationTemplate> GetAsync(CancellationToken cancellationToken);
}
