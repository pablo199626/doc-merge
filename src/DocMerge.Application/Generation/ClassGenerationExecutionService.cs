using DocMerge.Application.Files;
using DocMerge.Application.Validation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Application.Generation;

/// <summary>Coordinates generation, validation, integrity checks, and atomic batch publication.</summary>
public sealed class ClassGenerationExecutionService
{
    private readonly IClassDocumentGenerator _generator;
    private readonly IClassGenerationValidator _validator;
    private readonly IGenerationBatchPublisher _publisher;
    private readonly IFileStore _fileStore;

    /// <summary>Initializes a new instance of the <see cref="ClassGenerationExecutionService"/> class.</summary>
    public ClassGenerationExecutionService(
        IClassDocumentGenerator generator,
        IClassGenerationValidator validator,
        IGenerationBatchPublisher publisher,
        IFileStore fileStore)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    /// <summary>Generates, validates, and atomically publishes a prepared batch.</summary>
    /// <param name="prepared">The immutable prepared batch.</param>
    /// <param name="sortingProfile">The output sorting profile.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The published result and its structured issues.</returns>
    public async Task<ClassGenerationResult> ExecuteAsync(
        PreparedClassGeneration prepared,
        SortingProfile sortingProfile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(sortingProfile);
        if (prepared.Plan.HasBlockingIssues)
        {
            throw new OperationValidationException(
                "El plan contiene incidencias estructurales que impiden publicar el lote.");
        }

        var temporary = _publisher.CreateTemporaryDirectory(
            prepared.Request.OutputDirectory);
        try
        {
            var generationRequest = new GenerateClassDocumentsRequest(
                prepared, temporary, sortingProfile);
            var generated = await _generator.GenerateAsync(
                generationRequest, cancellationToken);
            await _validator.ValidateAsync(
                new ValidateClassDocumentsRequest(prepared, generated, sortingProfile),
                cancellationToken);
            await VerifyInputsAsync(prepared, cancellationToken);
            await _publisher.PublishAsync(
                temporary, prepared.Request.OutputDirectory, cancellationToken);
            var published = generated.Select(document => new GeneratedClassDocument(
                document.ClassIdentifier,
                Path.Combine(
                    prepared.Request.OutputDirectory,
                    Path.GetFileName(document.Path)))).ToArray();
            return new ClassGenerationResult(
                prepared.Request.OutputDirectory, published, prepared.Plan);
        }
        finally
        {
            _publisher.DeleteIfExists(temporary);
        }
    }

    private async Task VerifyInputsAsync(
        PreparedClassGeneration prepared,
        CancellationToken cancellationToken)
    {
        await VerifyHashAsync(
            prepared.Template.DocumentPath,
            prepared.Template.Sha256,
            cancellationToken);
        foreach (var historical in prepared.HistoricalDocuments)
        {
            await VerifyHashAsync(
                historical.DocumentPath, historical.Sha256, cancellationToken);
        }

        foreach (var classPlan in prepared.Plan.Classes)
        {
            await VerifyHashAsync(
                classPlan.Roster.SourcePath, classPlan.Roster.Sha256, cancellationToken);
        }
    }

    private async Task VerifyHashAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        var actual = await _fileStore.ComputeHashAsync(path, cancellationToken);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new TransferValidationException(
                $"Una entrada cambió durante la generación: {path}");
        }
    }
}
