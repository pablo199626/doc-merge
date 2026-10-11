namespace DocMerge.Infrastructure.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"docmerge_tests_{Guid.NewGuid():N}");

    internal TemporaryDirectory()
    {
        Directory.CreateDirectory(_path);
    }

    internal string RootPath => _path;

    internal string File(string name) => Path.Combine(_path, name);

    public void Dispose()
    {
        Directory.Delete(_path, recursive: true);
    }
}
