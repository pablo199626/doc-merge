using System.IO.Compression;

namespace DocMerge.Infrastructure.Docx;

internal sealed class DocxPackage
{
    private DocxPackage(
        Dictionary<string, byte[]> parts,
        IReadOnlyList<string> originalOrder)
    {
        Parts = parts;
        OriginalOrder = originalOrder;
    }

    internal Dictionary<string, byte[]> Parts { get; }

    internal IReadOnlyList<string> OriginalOrder { get; }

    internal static DocxPackage FromBytes(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var order = new List<string>(archive.Entries.Count);
        foreach (var entry in archive.Entries)
        {
            order.Add(entry.FullName);
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            parts.Add(entry.FullName, buffer.ToArray());
        }

        return new DocxPackage(parts, order);
    }

    internal async Task WriteAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"docmerge_{Guid.NewGuid():N}.docx");
        try
        {
            await WriteTemporaryAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private async Task WriteTemporaryAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in OriginalOrder.Concat(Parts.Keys))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!written.Add(name) || !Parts.TryGetValue(name, out var content))
            {
                continue;
            }

            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(content, cancellationToken);
        }
    }
}
