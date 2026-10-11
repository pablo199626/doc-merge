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
    private readonly IGenerationInputDiscovery _discovery;

    /// <summary>Initializes a new instance of the <see cref="ClassGenerationExecutionService"/> class.</summary>
    public ClassGenerationExecutionService(
        IClassDocumentGenerator generator,
        IClassGenerationValidator validator,
        IGenerationBatchPublisher publisher,
        IFileStore fileStore,
        IGenerationInputDiscovery discovery)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    /// <summary>Generates, validates, and atomically publishes a prepared batch.</summary>
    /// <param name="request">The reviewed batch, output directory, and sorting profile.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <param name="progress">Optional stage progress.</param>
    /// <returns>The published result and its structured issues.</returns>
    public async Task<ClassGenerationResult> ExecuteAsync(
        ClassGenerationExecutionRequest request,
        CancellationToken cancellationToken,
        IProgress<ClassGenerationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = request.Prepared;
        var sortingProfile = request.SortingProfile;
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        if (prepared.Plan.HasBlockingIssues)
        {
            throw new OperationValidationException(
                "El plan contiene incidencias estructurales que impiden publicar el lote.");
        }

        progress?.Report(new ClassGenerationProgress(ClassGenerationStage.VerifyingInputs));
        await VerifyInputsAsync(prepared, cancellationToken);
        var temporary = _publisher.CreateTemporaryDirectory(request.OutputDirectory);
        try
        {
            progress?.Report(new ClassGenerationProgress(ClassGenerationStage.GeneratingDocuments));
            var generationRequest = new GenerateClassDocumentsRequest(
                prepared, temporary, sortingProfile);
            var generated = await _generator.GenerateAsync(
                generationRequest, cancellationToken);
            progress?.Report(new ClassGenerationProgress(ClassGenerationStage.ValidatingDocuments));
            await _validator.ValidateAsync(
                new ValidateClassDocumentsRequest(prepared, generated, sortingProfile),
                cancellationToken);
            progress?.Report(new ClassGenerationProgress(ClassGenerationStage.VerifyingInputs));
            await VerifyInputsAsync(prepared, cancellationToken);
            progress?.Report(new ClassGenerationProgress(ClassGenerationStage.Publishing));
            await _publisher.PublishAsync(
                temporary, request.OutputDirectory, cancellationToken);
            var published = generated.Select(document => new GeneratedClassDocument(
                document.ClassIdentifier,
                Path.Combine(
                    request.OutputDirectory,
                    Path.GetFileName(document.Path)))).ToArray();
            progress?.Report(new ClassGenerationProgress(ClassGenerationStage.Completed));
            return new ClassGenerationResult(
                request.OutputDirectory, published, prepared.Plan);
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
        var discovered = await _discovery.DiscoverAsync(
            prepared.Request.HistoricalDirectory,
            prepared.Request.RosterDirectory,
            prepared.Request.TemplatePath,
            cancellationToken);
        VerifyDiscoveredPaths(prepared.Inputs, discovered);
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

    private static void VerifyDiscoveredPaths(
        GenerationInputSet expected,
        GenerationInputSet actual)
    {
        if (!expected.HistoricalDocumentPaths.SequenceEqual(
                actual.HistoricalDocumentPaths, StringComparer.OrdinalIgnoreCase)
            || !expected.RosterPdfPaths.SequenceEqual(
                actual.RosterPdfPaths, StringComparer.OrdinalIgnoreCase))
        {
            throw new PreparedInputsChangedException(
                "El conjunto de entradas cambió después de preparar el plan.");
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
            throw new PreparedInputsChangedException(
                $"Una entrada cambió durante la generación: {path}");
        }
    }
}
