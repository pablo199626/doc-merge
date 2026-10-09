namespace DocMerge.Application.Files;

public interface IFileStore
{
    Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken);

    string CreateTemporaryPath(string directory, string label);

    Task PublishAsync(string temporaryPath, string outputPath, CancellationToken cancellationToken);

    void DeleteIfExists(string path);
}
