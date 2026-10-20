using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using DocMerge.Application.Documents;
using DocMerge.Application.Validation;

namespace DocMerge.Infrastructure.Files;

public sealed class BundledGenerationTemplateProvider : IBundledGenerationTemplateProvider, IDisposable
{
    private const string ResourceName = "DocMerge.Infrastructure.Templates.StandardClassTemplate.v1.docx";
    private const string ExpectedSha256 = "f70a3e38d42268eda04c92f0442f2c489c2ce57bb033c042b7b9c8200baa9670";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _temporaryRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DocMerge",
        "Temp");
    private string? _sessionDirectory;
    private FileStream? _sessionLock;
    private BundledGenerationTemplate? _template;
    private bool _isDisposed;

    public async Task<BundledGenerationTemplate> GetAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_template is not null)
            {
                return _template;
            }

            Directory.CreateDirectory(_temporaryRoot);
            DeleteAbandonedSessions();
            _sessionDirectory = Path.Combine(
                _temporaryRoot,
                $".docmerge_template_{Environment.ProcessId}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_sessionDirectory);
            _sessionLock = new FileStream(
                Path.Combine(_sessionDirectory, ".active"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None);
            var path = Path.Combine(_sessionDirectory, "StandardClassTemplate.v1.docx");
            var content = await ReadResourceAsync(cancellationToken);
            VerifyHash(content);
            await File.WriteAllBytesAsync(path, content, cancellationToken);
            VerifyHash(await File.ReadAllBytesAsync(path, cancellationToken));
            _template = new BundledGenerationTemplate(
                "standard-class",
                1,
                "academic-measures-three-column",
                TemplateHeaderContract.Version,
                "Plantilla estándar de Talos",
                ExpectedSha256,
                path);
            return _template;
        }
        catch
        {
            DeleteSessionDirectory();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DeleteSessionDirectory();
        _gate.Dispose();
    }

    private static async Task<byte[]> ReadResourceAsync(CancellationToken cancellationToken)
    {
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new OperationValidationException(
                "No se encontró la plantilla predeterminada.");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static void VerifyHash(byte[] content)
    {
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actual, ExpectedSha256, StringComparison.Ordinal))
        {
            throw new OperationValidationException(
                "La plantilla predeterminada no supera la comprobación de integridad.");
        }
    }

    private void DeleteAbandonedSessions()
    {
        foreach (var directory in Directory.EnumerateDirectories(
            _temporaryRoot, ".docmerge_template_*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteAbandonedSession(directory);
        }
    }

    private static void TryDeleteAbandonedSession(string directory)
    {
        var lockPath = Path.Combine(directory, ".active");
        try
        {
            using (new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None))
            {
            }

            Directory.Delete(directory, recursive: true);
        }
        catch (IOException error)
        {
            // Another process owns this session.
            Debug.WriteLine(error);
        }
        catch (UnauthorizedAccessException error)
        {
            // Cleanup is best effort and must not prevent startup.
            Debug.WriteLine(error);
        }
    }

    private void DeleteSessionDirectory()
    {
        _sessionLock?.Dispose();
        _sessionLock = null;
        if (_sessionDirectory is null)
        {
            return;
        }

        try
        {
            Directory.Delete(_sessionDirectory, recursive: true);
        }
        catch (IOException error)
        {
            Debug.WriteLine(error);
        }
        catch (UnauthorizedAccessException error)
        {
            Debug.WriteLine(error);
        }

        _sessionDirectory = null;
        _template = null;
    }
}
