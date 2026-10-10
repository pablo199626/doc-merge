using System.Security.Cryptography;
using DocMerge.Application.Files;

namespace DocMerge.Infrastructure.Files;

public sealed class PhysicalFileStore : IFileStore
{
    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public async Task<string> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    public string CreateTemporaryPath(string outputPath, string label)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"docmerge_{Guid.NewGuid():N}_{label}.docx");
    }

    public Task PublishAsync(
        string temporaryPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporaryPath, outputPath, overwrite: true);
        return Task.CompletedTask;
    }

    public void DeleteIfExists(string path)
    {
        File.Delete(path);
    }
}
