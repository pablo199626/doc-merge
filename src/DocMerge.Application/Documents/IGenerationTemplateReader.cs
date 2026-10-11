namespace DocMerge.Application.Documents;

/// <summary>Reads and validates a DOCX template used to generate class documents.</summary>
public interface IGenerationTemplateReader
{
    /// <summary>Reads the template structure and its empty prototype row.</summary>
    /// <param name="path">The template path.</param>
    /// <param name="header">The exact student-name header.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated template snapshot.</returns>
    Task<GenerationTemplateSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken);
}
