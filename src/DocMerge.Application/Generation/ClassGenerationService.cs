using DocMerge.Application.Documents;
using DocMerge.Application.Errors;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Validation;
using DocMerge.Domain.Generation;

namespace DocMerge.Application.Generation;

/// <summary>Coordinates discovery, reading, indexing, and planning for class generation.</summary>
public sealed class ClassGenerationService
{
    private readonly IGenerationInputDiscovery _discovery;
    private readonly IClassRosterReader _rosterReader;
    private readonly IGenerationTemplateReader _templateReader;
    private readonly IHistoricalStudentReader _historicalReader;
    private readonly ClassGenerationPlanner _planner;

    /// <summary>Initializes a new instance of the <see cref="ClassGenerationService"/> class.</summary>
    public ClassGenerationService(
        IGenerationInputDiscovery discovery,
        IClassRosterReader rosterReader,
        IGenerationTemplateReader templateReader,
        IHistoricalStudentReader historicalReader,
        ClassGenerationPlanner planner)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _rosterReader = rosterReader ?? throw new ArgumentNullException(nameof(rosterReader));
        _templateReader = templateReader ?? throw new ArgumentNullException(nameof(templateReader));
        _historicalReader = historicalReader ?? throw new ArgumentNullException(nameof(historicalReader));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    /// <summary>Reads every input once and creates an immutable class-generation plan.</summary>
    /// <param name="request">The class-generation request.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <param name="progress">Optional stage progress.</param>
    /// <returns>The prepared inputs and generation plan.</returns>
    public async Task<PreparedClassGeneration> PrepareAsync(
        ClassGenerationRequest request,
        CancellationToken cancellationToken,
        IProgress<ClassGenerationProgress>? progress = null)
    {
        ValidateRequest(request);
        progress?.Report(new ClassGenerationProgress(ClassGenerationStage.DiscoveringInputs));
        var inputs = await DiscoverAsync(request, cancellationToken);
        progress?.Report(new ClassGenerationProgress(ClassGenerationStage.ReadingTemplate));
        var template = await ReadTemplateAsync(request, cancellationToken);
        var rosters = await ReadRostersAsync(inputs.RosterPdfPaths, cancellationToken, progress);
        var historical = await ReadHistoricalAsync(
            inputs.HistoricalDocumentPaths, request.Header, cancellationToken, progress);
        try
        {
            ValidateSchemas(template, historical);
        }
        catch (OperationValidationException error)
        {
            throw new GenerationInputException(
                GenerationInputType.InputSet, null, error.Message, error);
        }
        progress?.Report(new ClassGenerationProgress(ClassGenerationStage.Planning));
        var index = new HistoricalStudentIndex(
            historical.SelectMany(document => document.Candidates).ToArray());
        var plan = _planner.Create(rosters, index);
        return new PreparedClassGeneration(request, inputs, template, historical, plan);
    }

    private async Task<GenerationInputSet> DiscoverAsync(
        ClassGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _discovery.DiscoverAsync(
                request.HistoricalDirectory,
                request.RosterDirectory,
                request.TemplatePath,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GenerationInputException)
        {
            throw;
        }
        catch (Exception error) when (error is DocMergeException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            throw new GenerationInputException(
                GenerationInputType.InputSet, null, error.Message, error);
        }
    }

    private async Task<GenerationTemplateSnapshot> ReadTemplateAsync(
        ClassGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _templateReader.ReadAsync(
                request.TemplatePath, request.Header, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is DocMergeException
            or IOException
            or UnauthorizedAccessException)
        {
            throw new GenerationInputException(
                GenerationInputType.Template, request.TemplatePath, error.Message, error);
        }
    }

    private async Task<IReadOnlyList<ClassRoster>> ReadRostersAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken,
        IProgress<ClassGenerationProgress>? progress)
    {
        var rosters = new List<ClassRoster>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                rosters.Add(await _rosterReader.ReadAsync(path, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error) when (error is DocMergeException
                or IOException
                or UnauthorizedAccessException)
            {
                throw new GenerationInputException(
                    GenerationInputType.Rosters, path, error.Message, error);
            }
            progress?.Report(new ClassGenerationProgress(
                ClassGenerationStage.ReadingRosters, rosters.Count, paths.Count));
        }

        return rosters;
    }

    private async Task<IReadOnlyList<HistoricalDocumentSnapshot>> ReadHistoricalAsync(
        IReadOnlyList<string> paths,
        string header,
        CancellationToken cancellationToken,
        IProgress<ClassGenerationProgress>? progress)
    {
        var documents = new List<HistoricalDocumentSnapshot>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                documents.Add(await _historicalReader.ReadAsync(path, header, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error) when (error is DocMergeException
                or IOException
                or UnauthorizedAccessException)
            {
                throw new GenerationInputException(
                    GenerationInputType.HistoricalDocuments, path, error.Message, error);
            }
            progress?.Report(new ClassGenerationProgress(
                ClassGenerationStage.ReadingHistoricalDocuments, documents.Count, paths.Count));
        }

        return documents;
    }

    private static void ValidateSchemas(
        GenerationTemplateSnapshot template,
        IReadOnlyList<HistoricalDocumentSnapshot> historical)
    {
        foreach (var document in historical)
        {
            if (!template.ColumnHeaders.SequenceEqual(
                document.ColumnHeaders, StringComparer.Ordinal))
            {
                throw new OperationValidationException(
                    $"El documento histórico no tiene las mismas columnas que la plantilla: " +
                    document.DocumentPath);
            }
        }
    }

    private static void ValidateRequest(ClassGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Header))
        {
            throw new OperationValidationException("La cabecera no puede estar vacía.");
        }

        if (!File.Exists(request.TemplatePath))
        {
            throw new OperationValidationException(
                $"No existe la plantilla DOCX: {request.TemplatePath}");
        }
    }
}
