using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.Services;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed class StudentDetailViewModelTests
{
    [Fact]
    public void Open_FoundStudent_ShowsIncludedMeasuresAndProvenance()
    {
        var sourceService = new StubSourceDocumentService();
        var viewModel = new StudentDetailViewModel(sourceService);
        var candidate = Candidate("history.docx", "MEDIDA HISTÓRICA");
        var student = Student(
            "ALUMNO, ENCONTRADO",
            "10000001",
            HistoricalResolutionType.Found,
            [candidate],
            candidate);

        viewModel.Open(student, [student]);

        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.IsIncluded);
        Assert.Equal("Se incluirá en el documento", viewModel.OutcomeTitle);
        Assert.True(viewModel.HasMeasures);
        Assert.Contains(
            viewModel.MeasuresContent.Paragraphs.SelectMany(paragraph => paragraph.Inlines),
            inline => inline.Text == "MEDIDA HISTÓRICA");
        Assert.Equal(2, viewModel.Provenance.Count);
    }

    [Fact]
    public void Open_NewStudent_ShowsValidEmptyMeasuresState()
    {
        var viewModel = new StudentDetailViewModel(new StubSourceDocumentService());
        var student = Student(
            "ALUMNO, NUEVO",
            "10000002",
            HistoricalResolutionType.NewStudent,
            [],
            selectedCandidate: null);

        viewModel.Open(student, [student]);

        Assert.True(viewModel.IsIncluded);
        Assert.False(viewModel.HasMeasures);
        Assert.Equal("No hay medidas registradas", viewModel.MeasuresEmptyMessage);
        Assert.Empty(viewModel.Issues);
    }

    [Fact]
    public void CandidateNavigation_Conflict_InspectsWithoutResolving()
    {
        var viewModel = new StudentDetailViewModel(new StubSourceDocumentService());
        var first = Candidate("first.docx", "PRIMERA");
        var second = Candidate("second.docx", "SEGUNDA");
        var student = Student(
            "ALUMNO, CONFLICTO",
            "10000003",
            HistoricalResolutionType.Conflict,
            [first, second],
            selectedCandidate: null);

        viewModel.Open(student, [student]);
        viewModel.NextCandidateCommand.Execute(parameter: null);

        Assert.False(viewModel.IsIncluded);
        Assert.Equal("Este alumno no se incluirá en el documento", viewModel.OutcomeTitle);
        Assert.Same(second, viewModel.InspectedCandidate);
        Assert.Null(student.Plan.Resolution.SelectedCandidate);
    }

    [Fact]
    public void UpdateVisibleStudents_SelectedStudentExcluded_ClosesDetail()
    {
        var viewModel = new StudentDetailViewModel(new StubSourceDocumentService());
        var first = Student("ALUMNO, UNO", "10000001", HistoricalResolutionType.NewStudent, [], null);
        var second = Student("ALUMNO, DOS", "10000002", HistoricalResolutionType.NewStudent, [], null);
        viewModel.Open(first, [first, second]);

        viewModel.UpdateVisibleStudents([second]);

        Assert.False(viewModel.IsOpen);
        Assert.Null(viewModel.Student);
    }

    [Fact]
    public void NextStudentCommand_FilteredOrder_ChangesStudentAndUpdatesPosition()
    {
        var viewModel = new StudentDetailViewModel(new StubSourceDocumentService());
        var first = Student("ALUMNO, UNO", "10000001", HistoricalResolutionType.NewStudent, [], null);
        var second = Student("ALUMNO, DOS", "10000002", HistoricalResolutionType.NewStudent, [], null);
        viewModel.Open(first, [first, second]);

        viewModel.NextStudentCommand.Execute(parameter: null);

        Assert.Same(second, viewModel.Student);
        Assert.Equal("2 de 2", viewModel.PositionText);
        Assert.False(viewModel.CanNavigateNext);
        Assert.True(viewModel.CanNavigatePrevious);
    }

    [Fact]
    public void OpenSourceCommand_ValidSource_DelegatesExactPath()
    {
        var sourceService = new StubSourceDocumentService();
        var viewModel = new StudentDetailViewModel(sourceService);
        var source = new StudentSourceViewModel("Listado PDF", "2A.pdf", "C:\\sources\\2A.pdf", "página 1");

        viewModel.OpenSourceCommand.Execute(source);

        Assert.Equal(source.Path, sourceService.OpenedPath);
        Assert.Null(viewModel.SourceOpenError);
    }

    private static StudentReviewViewModel Student(
        string name,
        string number,
        HistoricalResolutionType type,
        IReadOnlyList<HistoricalStudentCandidate> candidates,
        HistoricalStudentCandidate? selectedCandidate)
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "A");
        var entry = new ClassRosterEntry(
            name,
            name,
            StudentNameMatchKey.Create(name),
            number,
            1,
            1);
        var resolution = new HistoricalResolution(type, candidates, selectedCandidate);
        return new StudentReviewViewModel(
            classIdentifier,
            new StudentRowPlan(entry, resolution),
            [],
            "C:\\sources\\2A.pdf");
    }

    private static HistoricalStudentCandidate Candidate(string path, string text)
    {
        var content = new HistoricalRichContent(
        [
            new HistoricalRichParagraph(
                [new HistoricalRichInline(
                    HistoricalRichInlineKind.Text,
                    text,
                    HistoricalTextFormatting.Default)],
                HistoricalParagraphFormatting.Default),
        ]);
        var record = new DocumentRecord(
            $"C:\\sources\\{path}",
            0,
            1,
            "ALUMNO, FICTICIO",
            "alumno, ficticio",
            "<w:tr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" />",
            RecordDependencies.Empty);
        return new HistoricalStudentCandidate(
            record,
            "alumno ficticio",
            [new HistoricalInformationField("Medidas", text, false, content)]);
    }

    private sealed class StubSourceDocumentService : ISourceDocumentService
    {
        internal string? OpenedPath { get; private set; }

        public void Open(string path) => OpenedPath = path;
    }
}
