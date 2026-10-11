namespace DocMerge.Application.Files;

/// <summary>Creates, publishes, and cleans complete class-generation batch directories.</summary>
public interface IGenerationBatchPublisher
{
    /// <summary>Creates a private temporary directory beside the final output directory.</summary>
    /// <param name="outputDirectory">The final output directory.</param>
    /// <returns>The newly created temporary directory.</returns>
    string CreateTemporaryDirectory(string outputDirectory);

    /// <summary>Atomically publishes a complete temporary directory.</summary>
    /// <param name="temporaryDirectory">The validated temporary directory.</param>
    /// <param name="outputDirectory">The new final output directory.</param>
    /// <param name="cancellationToken">The token that cancels publication before it begins.</param>
    /// <returns>A task representing publication.</returns>
    Task PublishAsync(
        string temporaryDirectory,
        string outputDirectory,
        CancellationToken cancellationToken);

    /// <summary>Deletes a temporary directory when it exists.</summary>
    /// <param name="temporaryDirectory">The temporary directory.</param>
    void DeleteIfExists(string temporaryDirectory);
}
