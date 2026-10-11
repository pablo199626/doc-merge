using DocMerge.Application.Files;
using DocMerge.Application.Validation;

namespace DocMerge.Infrastructure.Files;

public sealed class GenerationBatchPublisher : IGenerationBatchPublisher
{
    public string CreateTemporaryDirectory(string outputDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new OperationValidationException(
                $"Ya existe la ruta de salida del lote: {output}");
        }

        var parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(
            parent, $".docmerge_{Guid.NewGuid():N}_{Path.GetFileName(output)}");
        Directory.CreateDirectory(temporary);
        return temporary;
    }

    public Task PublishAsync(
        string temporaryDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new OperationValidationException(
                $"Ya existe la ruta de salida del lote: {output}");
        }

        Directory.Move(temporaryDirectory, output);
        return Task.CompletedTask;
    }

    public void DeleteIfExists(string temporaryDirectory)
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
