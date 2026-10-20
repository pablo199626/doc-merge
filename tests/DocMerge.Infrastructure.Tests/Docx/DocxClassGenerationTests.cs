using System.Text;
using DocMerge.Application.Documents;
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
    public async Task ReadAsync_RichHistoricalMeasures_PreservesFormattingAndUnsupportedNotice()
    {
        using var temporary = new TemporaryDirectory();
        var historicalPath = temporary.File("historical.docx");
        const string richRow = "<w:tr><w:tc><w:p/></w:tc>"
            + "<w:tc><w:p><w:r><w:t>Alumno Uno</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"120\" w:after=\"80\"/>"
            + "<w:ind w:left=\"240\" w:firstLine=\"100\"/></w:pPr>"
            + "<w:r><w:rPr><w:b/><w:i/><w:u w:val=\"single\"/><w:rFonts w:ascii=\"Arial\"/>"
            + "<w:sz w:val=\"20\"/><w:color w:val=\"123456\"/><w:highlight w:val=\"yellow\"/>"
            + "</w:rPr><w:t>Primera</w:t><w:br/><w:tab/><w:t>Segunda</w:t></w:r>"
            + "<w:object/></w:p></w:tc></w:tr>";
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + richRow],
            marker: "HISTORICAL",
            source: true);

        var snapshot = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath,
            "Nombre y Apellidos",
            CancellationToken.None);

        var field = Assert.Single(
            Assert.Single(snapshot.Candidates).ReviewFields,
            item => item.Header == "Medidas");
        var paragraph = Assert.Single(field.PreviewContent.Paragraphs);
        var text = Assert.Single(paragraph.Inlines, inline => inline.Text == "Primera");
        Assert.Equal(HistoricalParagraphAlignment.Center, paragraph.Formatting.Alignment);
        Assert.Equal(12, paragraph.Formatting.LeftIndentPoints);
        Assert.Equal(5, paragraph.Formatting.FirstLineIndentPoints);
        Assert.Equal(6, paragraph.Formatting.SpaceBeforePoints);
        Assert.Equal(4, paragraph.Formatting.SpaceAfterPoints);
        Assert.True(text.Formatting.IsBold);
        Assert.True(text.Formatting.IsItalic);
        Assert.True(text.Formatting.IsUnderlined);
        Assert.Equal("Arial", text.Formatting.FontFamily);
        Assert.Equal(10, text.Formatting.FontSizePoints);
        Assert.Equal("123456", text.Formatting.ForegroundColor);
        Assert.Contains(paragraph.Inlines, inline => inline.Kind == HistoricalRichInlineKind.LineBreak);
        Assert.Contains(paragraph.Inlines, inline => inline.Kind == HistoricalRichInlineKind.Tab);
        Assert.Contains(paragraph.Inlines, inline => inline.Kind == HistoricalRichInlineKind.UnsupportedContent);
        Assert.Contains("<w:object", snapshot.Candidates[0].Record.RowXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_NumberedHistoricalMeasure_ResolvesListDescriptor()
    {
        using var temporary = new TemporaryDirectory();
        var historicalPath = temporary.File("historical.docx");
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            marker: "HISTORICAL",
            source: true);

        var snapshot = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath,
            "Nombre y Apellidos",
            CancellationToken.None);

        var field = Assert.Single(
            Assert.Single(snapshot.Candidates).ReviewFields,
            item => item.Header == "Medidas");
        var list = Assert.Single(field.PreviewContent.Paragraphs).List;
        Assert.NotNull(list);
        Assert.Equal("decimal", list.Format);
        Assert.Equal("%1.", list.LevelText);
        var text = Assert.Single(field.PreviewContent.Paragraphs[0].Inlines);
        Assert.Equal("FF0000", text.Formatting.ForegroundColor);
    }

    [Fact]
    public async Task ReadAsync_StyleCascadeAndBooleanOverride_ResolvesEffectiveFormatting()
    {
        using var temporary = new TemporaryDirectory();
        var historicalPath = temporary.File("historical.docx");
        const string row = "<w:tr><w:tc><w:p/></w:tc>"
            + "<w:tc><w:p><w:r><w:t>Alumno Uno</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:pPr><w:pStyle w:val=\"Derived\"/></w:pPr>"
            + "<w:r><w:rPr><w:rStyle w:val=\"Emphasis\"/><w:b w:val=\"0\"/></w:rPr>"
            + "<w:t xml:space=\"preserve\"> medida </w:t></w:r></w:p></w:tc></w:tr>";
        DocxTestPackage.Create(historicalPath, [DocxTestPackage.Header + row], source: true);
        DocxTestPackage.ReplacePart(
            historicalPath,
            "word/styles.xml",
            $"<w:styles xmlns:w=\"{DocxTestPackage.WordNamespace}\">"
            + "<w:docDefaults><w:rPrDefault><w:rPr><w:b/><w:rFonts w:ascii=\"Fallback Font\"/></w:rPr></w:rPrDefault></w:docDefaults>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Base\"><w:rPr><w:i/></w:rPr></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Derived\"><w:basedOn w:val=\"Base\"/><w:rPr><w:u w:val=\"single\"/></w:rPr></w:style>"
            + "<w:style w:type=\"character\" w:styleId=\"Emphasis\"><w:rPr><w:color w:val=\"ABCDEF\"/></w:rPr></w:style>"
            + "</w:styles>");

        var snapshot = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath,
            "Nombre y Apellidos",
            CancellationToken.None);

        var field = Assert.Single(
            Assert.Single(snapshot.Candidates).ReviewFields,
            item => item.Header == "Medidas");
        var inline = Assert.Single(Assert.Single(field.PreviewContent.Paragraphs).Inlines);
        Assert.Equal(" medida ", inline.Text);
        Assert.False(inline.Formatting.IsBold);
        Assert.True(inline.Formatting.IsItalic);
        Assert.True(inline.Formatting.IsUnderlined);
        Assert.Equal("Fallback Font", inline.Formatting.FontFamily);
        Assert.Equal("ABCDEF", inline.Formatting.ForegroundColor);
    }

    [Fact]
    public async Task ReadAsync_NonIdentityCells_PreservesPhysicalOrder()
    {
        using var temporary = new TemporaryDirectory();
        var historicalPath = temporary.File("historical.docx");
        const string header = "<w:tr><w:tc><w:p><w:r><w:t>Primera</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:r><w:t>Nombre y Apellidos</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:r><w:t>Segunda</w:t></w:r></w:p></w:tc></w:tr>";
        const string row = "<w:tr><w:tc><w:p><w:r><w:t>UNO</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:r><w:t>Alumno Uno</w:t></w:r></w:p></w:tc>"
            + "<w:tc><w:p><w:r><w:t>DOS</w:t></w:r></w:p></w:tc></w:tr>";
        DocxTestPackage.Create(historicalPath, [header + row], source: true);

        var snapshot = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath,
            "Nombre y Apellidos",
            CancellationToken.None);

        var fields = Assert.Single(snapshot.Candidates).ReviewFields;
        Assert.Equal(["Primera", "Segunda"], fields.Select(field => field.Header));
        Assert.Equal(["UNO", "DOS"], fields.Select(field => field.Text));
    }

    [Fact]
    public async Task ReadAsync_CyclicStyle_ThrowsUsefulDocumentStructureException()
    {
        using var temporary = new TemporaryDirectory();
        var historicalPath = temporary.File("historical.docx");
        DocxTestPackage.Create(
            historicalPath,
            [DocxTestPackage.Header + DocxTestPackage.PersonRow("Alumno Uno", "MEDIDA")],
            source: true);
        DocxTestPackage.ReplacePart(
            historicalPath,
            "word/styles.xml",
            $"<w:styles xmlns:w=\"{DocxTestPackage.WordNamespace}\">"
            + "<w:style w:type=\"paragraph\" w:styleId=\"EstiloPersona\"><w:basedOn w:val=\"Circular\"/></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Circular\"><w:basedOn w:val=\"EstiloPersona\"/></w:style>"
            + "</w:styles>");

        var error = await Assert.ThrowsAsync<DocumentStructureException>(() =>
            new DocxHistoricalStudentReader().ReadAsync(
                historicalPath,
                "Nombre y Apellidos",
                CancellationToken.None));

        Assert.Contains("ciclo de estilos", error.Message, StringComparison.OrdinalIgnoreCase);
    }

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
    public async Task GenerateAndValidateAsync_ConflictedStudent_OmitsRowFromDocument()
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
            [DocxTestPackage.Header
                + DocxTestPackage.PersonRow("Alumno Uno", "PRIMERA")
                + DocxTestPackage.PersonRow("Alumno Uno", "SEGUNDA")],
            marker: "HISTORICAL",
            source: true);
        var prepared = await PrepareAsync(templatePath, historicalPath);
        var profile = SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier);

        var generated = await new DocxClassDocumentGenerator().GenerateAsync(
            new GenerateClassDocumentsRequest(prepared, temporary.File("working"), profile),
            CancellationToken.None);
        await new DocxClassGenerationValidator(new DocxDocumentReader()).ValidateAsync(
            new ValidateClassDocumentsRequest(prepared, generated, profile),
            CancellationToken.None);
        var result = await new DocxDocumentReader().ReadAsync(
            Assert.Single(generated).Path, "Nombre y Apellidos", CancellationToken.None);

        Assert.Equal("ALUMNO, DOS", Assert.Single(result.Records).OriginalName);
        Assert.Equal(1, prepared.Plan.OmittedStudentCount);
    }

    [Fact]
    public async Task ExecuteAsync_AllStudentsConflicted_RejectsBatchWithoutPublishing()
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
            [DocxTestPackage.Header
                + DocxTestPackage.PersonRow("Alumno Uno", "PRIMERA")
                + DocxTestPackage.PersonRow("Alumno Uno", "SEGUNDA")],
            marker: "HISTORICAL",
            source: true);
        var prepared = await PrepareAsync(
            templatePath,
            historicalPath,
            rosterEntries: [Entry("11111111", "ALUMNO, UNO", 1)]);
        var service = new ClassGenerationExecutionService(
            new DocxClassDocumentGenerator(),
            new DocxClassGenerationValidator(new DocxDocumentReader()),
            new GenerationBatchPublisher(),
            new PhysicalFileStore(),
            new StaticInputDiscovery(prepared.Inputs));

        var error = await Assert.ThrowsAsync<DocMerge.Application.Validation.OperationValidationException>(() =>
            service.ExecuteAsync(
                new ClassGenerationExecutionRequest(
                    prepared,
                    outputPath,
                    SortingProfiles.Get(SortingProfiles.CommaSeparatedIdentifier)),
                CancellationToken.None));

        Assert.Contains("No hay alumnos", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputPath));
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
        bool includeSecondClass = false,
        IReadOnlyList<ClassRosterEntry>? rosterEntries = null)
    {
        var template = await new DocxTemplateReader().ReadAsync(
            templatePath, "Nombre y Apellidos", CancellationToken.None);
        var historical = await new DocxHistoricalStudentReader().ReadAsync(
            historicalPath, "Nombre y Apellidos", CancellationToken.None);
        var entries = rosterEntries ??
        [
            Entry("11111111", "ALUMNO, UNO", 1),
            Entry("22222222", "ALUMNO, DOS", 2),
        ];
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
