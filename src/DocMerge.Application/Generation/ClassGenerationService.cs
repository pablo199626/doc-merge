using DocMerge.Application.Documents;
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
    /// <returns>The prepared inputs and generation plan.</returns>
    public async Task<PreparedClassGeneration> PrepareAsync(
        ClassGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var inputs = _discovery.Discover(
            request.HistoricalDirectory, request.RosterDirectory, request.TemplatePath);
        var template = await _templateReader.ReadAsync(
            request.TemplatePath, request.Header, cancellationToken);
        var rosters = await ReadRostersAsync(inputs.RosterPdfPaths, cancellationToken);
        var historical = await ReadHistoricalAsync(
            inputs.HistoricalDocumentPaths, request.Header, cancellationToken);
        ValidateSchemas(template, historical);
        var index = new HistoricalStudentIndex(
            historical.SelectMany(document => document.Candidates).ToArray());
        var plan = _planner.Create(rosters, index);
        return new PreparedClassGeneration(request, inputs, template, historical, plan);
    }

    private async Task<IReadOnlyList<ClassRoster>> ReadRostersAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var rosters = new List<ClassRoster>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rosters.Add(await _rosterReader.ReadAsync(path, cancellationToken));
        }

        return rosters;
    }

    private async Task<IReadOnlyList<HistoricalDocumentSnapshot>> ReadHistoricalAsync(
        IReadOnlyList<string> paths,
        string header,
        CancellationToken cancellationToken)
    {
        var documents = new List<HistoricalDocumentSnapshot>(paths.Count);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documents.Add(await _historicalReader.ReadAsync(path, header, cancellationToken));
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

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new OperationValidationException("La carpeta de salida no puede estar vacía.");
        }

        if (!File.Exists(request.TemplatePath))
        {
            throw new OperationValidationException(
                $"No existe la plantilla DOCX: {request.TemplatePath}");
        }
    }
}
