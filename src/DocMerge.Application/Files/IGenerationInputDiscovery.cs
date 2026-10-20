namespace DocMerge.Application.Files;

/// <summary>Discovers class-generation inputs without assigning historical classes.</summary>
public interface IGenerationInputDiscovery
{
    /// <summary>Discovers historical DOCX and class PDF files.</summary>
    /// <param name="historicalDirectory">The recursively searched historical root.</param>
    /// <param name="rosterDirectory">The recursively searched roster root.</param>
    /// <param name="templatePath">The template path to exclude from historical inputs.</param>
    /// <param name="cancellationToken">The token that cancels discovery.</param>
    /// <returns>The deterministic discovered inputs.</returns>
    Task<GenerationInputSet> DiscoverAsync(
        string historicalDirectory,
        string rosterDirectory,
        string templatePath,
        CancellationToken cancellationToken);
}
