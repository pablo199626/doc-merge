namespace DocMerge.Application.Files;

/// <summary>Abstracts path handling, hashing, temporary files, and atomic publication.</summary>
public interface IFileStore
{
    /// <summary>Resolves a path to its absolute representation.</summary>
    string GetFullPath(string path);

    /// <summary>Computes the lowercase SHA-256 hash of a file.</summary>
    Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken);

    /// <summary>Creates a unique temporary path beside an output file.</summary>
    string CreateTemporaryPath(string outputPath, string label);

    /// <summary>Atomically publishes a validated temporary file.</summary>
    Task PublishAsync(string temporaryPath, string outputPath, CancellationToken cancellationToken);

    /// <summary>Deletes a file when it exists.</summary>
    void DeleteIfExists(string path);
}
