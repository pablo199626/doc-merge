using System.Text;
using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Sorting;
using DocMerge.Infrastructure.Docx;
using DocMerge.Infrastructure.Files;

namespace DocMerge.Infrastructure.Tests.Docx;

public sealed class DocxClassGenerationTests
{
    [Fact]
    public async Task GenerateAndValidateAsync_FoundAndMissingStudents_PreservesDataWithoutHighlightingMissing()
    {
        using var temporary = new TemporaryDirectory();
        var templatePath = temporary.File("template.docx");
        var historicalPath = temporary.File("historical.docx");
        var workingPath = temporary.File("working");
        DocxTestPackage.Create(
            templatePath,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            marker: "TEMPLATE");
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            marker: "HISTORICAL",
            source: true);
        var reader = new DocxDocumentReader();
        var prepared = await PrepareAsync(templatePath, historicalPath);
        var profile = SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier);
        var generator = new DocxClassDocumentGenerator();

        var candidate = Assert.Single(prepared.HistoricalDocuments[0].Candidates);
        Assert.Contains(candidate.ReviewFields, field => field.Text == "MEDIDA");

        var generated = await generator.GenerateAsync(
            new GenerateClassDocumentsRequest(prepared, workingPath, profile),
            CancellationToken.None);
        await new DocxClassGenerationValidator(reader).ValidateAsync(
            new ValidateClassDocumentsRequest(prepared, generated, profile),
            CancellationToken.None);
        var result = await reader.ReadAsync(
            generated[0].Path, "Nombre y Apellidos", CancellationToken.None);

        Assert.Equal(["ALUMNO, DOS", "ALUMNO, UNO"],
            result.Records.Select(record => record.OriginalName));
        Assert.Contains("MEDIDA", result.Records[1].RowXml, StringComparison.Ordinal);
        Assert.DoesNotContain("<w:shd", result.Records[0].RowXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidBatch_PublishesNewDirectoryAtomically()
    {
        using var temporary = new TemporaryDirectory();
        var templatePath = temporary.File("template.docx");
        var historicalPath = temporary.File("historical.docx");
        var outputPath = temporary.File("published");
        DocxTestPackage.Create(
            templatePath,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            marker: "TEMPLATE");
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            marker: "HISTORICAL",
            source: true);
        var prepared = await PrepareAsync(templatePath, historicalPath);
        var reader = new DocxDocumentReader();
        var service = new ClassGenerationExecutionService(
            new DocxClassDocumentGenerator(),
            new DocxClassGenerationValidator(reader),
            new GenerationBatchPublisher(),
            new PhysicalFileStore(),
            new StaticInputDiscovery(prepared.Inputs));

        var result = await service.ExecuteAsync(
            new ClassGenerationExecutionRequest(
                prepared,
                outputPath,
                SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier)),
            CancellationToken.None);

        Assert.Equal(Path.GetFullPath(outputPath), Path.GetFullPath(result.OutputDirectory));
        Assert.True(File.Exists(Path.Combine(outputPath, "2A.docx")));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(Path.GetDirectoryName(templatePath)!),
            path => Path.GetFileName(path).StartsWith(".docmerge_", StringComparison.Ordinal));
    }

    private static async Task<PreparedClassGeneration> PrepareAsync(
        string templatePath,
        string historicalPath,
        bool includeSecondClass = false)
    {
        var template = await new DocxTemplateReader().ReadAsync(
            templatePath, "Nombre y Apellidos", CancellationToken.None);
        var historical = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath, "Nombre y Apellidos", CancellationToken.None);
        var entries = new[]
        {
            Entry("11111111", "ALUMNO, UNO", 1),
            Entry("22222222", "ALUMNO, DOS", 2),
        };
        var roster = new ClassRoster(
            templatePath,
            template.Sha256,
            new ClassIdentifier("ESO", 2, "A"),
            entries,
            1,
            hasDeclaredTotal: false);
        var rosters = includeSecondClass
            ? new[]
            {
                roster,
                new ClassRoster(
                    templatePath + ".second.pdf",
                    template.Sha256,
                    new ClassIdentifier("ESO", 2, "B"),
                    entries,
                    1,
                    hasDeclaredTotal: false),
            }
            : [roster];
        var planner = new ClassGenerationPlanner(new HistoricalStudentResolver());
        var plan = planner.Create(
            rosters, new HistoricalStudentIndex(historical.Candidates));
        var request = new ClassGenerationRequest(
            Path.GetDirectoryName(historicalPath)!,
            Path.GetDirectoryName(templatePath)!,
            templatePath,
            "Nombre y Apellidos");
        return new PreparedClassGeneration(
            request,
            new GenerationInputSet([historicalPath], [templatePath]),
            template,
            [historical],
            plan);
    }

    private static ClassRosterEntry Entry(string number, string name, int ordinal)
    {
        return new ClassRosterEntry(
            name,
            DocMerge.Domain.Identity.IdentityNormalizer.Normalize(name),
            StudentNameMatchKey.Create(name),
            number,
            1,
            ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DiscoveredInputsChanged_RejectsReviewedPlanBeforeGeneration()
    {
        using var temporary = new TemporaryDirectory();
        var templatePath = temporary.File("template.docx");
        var historicalPath = temporary.File("historical.docx");
        DocxTestPackage.Create(
            templatePath,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            marker: "TEMPLATE");
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            marker: "HISTORICAL",
            source: true);
        var prepared = await PrepareAsync(templatePath, historicalPath);
        var changedInputs = new GenerationInputSet(
            [.. prepared.Inputs.HistoricalDocumentPaths, temporary.File("added.docx")],
            prepared.Inputs.RosterPdfPaths);
        var service = new ClassGenerationExecutionService(
            new DocxClassDocumentGenerator(),
            new DocxClassGenerationValidator(new DocxDocumentReader()),
            new GenerationBatchPublisher(),
            new PhysicalFileStore(),
            new StaticInputDiscovery(changedInputs));
        var request = new ClassGenerationExecutionRequest(
            prepared,
            temporary.File("output"),
            SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier));

        await Assert.ThrowsAsync<PreparedInputsChangedException>(() =>
            service.ExecuteAsync(request, CancellationToken.None));

        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    [Fact]
    public async Task GenerateAndValidateAsync_MultipleClasses_WritesEachConfirmedHeader()
    {
        using var temporary = new TemporaryDirectory();
        var templatePath = temporary.File("template.docx");
        var historicalPath = temporary.File("historical.docx");
        DocxTestPackage.Create(
            templatePath,
            [DocxTestPackage.Header + DocxTestPackage.EmptyRow],
            documentHeaderFields: true);
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            source: true);
        var prepared = await PrepareAsync(templatePath, historicalPath, includeSecondClass: true);
        var headers = prepared.Plan.Classes.ToDictionary(
            item => item.Roster.ClassIdentifier,
            item => item.Roster.ClassIdentifier.Group == "A"
                ? new ClassHeaderValues("2 ESO A & apoyo", "María <Pérez>")
                : new ClassHeaderValues("2 ESO B", string.Empty));
        var options = new ClassGenerationOutputOptions(prepared, headers);
        var profile = SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier);

        var generated = await new DocxClassDocumentGenerator().GenerateAsync(
            new GenerateClassDocumentsRequest(prepared, temporary.File("working"), profile, options),
            CancellationToken.None);
        await new DocxClassGenerationValidator(new DocxDocumentReader()).ValidateAsync(
            new ValidateClassDocumentsRequest(prepared, generated, profile, options),
            CancellationToken.None);

        Assert.Equal(2, generated.Count);
        var firstXml = Encoding.UTF8.GetString(
            DocxTestPackage.ReadParts(generated.Single(item =>
                item.ClassIdentifier.Group == "A").Path)["word/document.xml"]);
        Assert.Contains("2 ESO A &amp; apoyo", firstXml, StringComparison.Ordinal);
        Assert.Contains("María &lt;Pérez&gt;", firstXml, StringComparison.Ordinal);
    }

    private sealed class StaticInputDiscovery(GenerationInputSet inputs) : IGenerationInputDiscovery
    {
        public Task<GenerationInputSet> DiscoverAsync(
            string historicalDirectory,
            string rosterDirectory,
            string templatePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(inputs);
        }
    }
}
