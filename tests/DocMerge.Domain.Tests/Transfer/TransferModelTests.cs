using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;
using DocMerge.Domain.Transfer;

namespace DocMerge.Domain.Tests.Transfer;

public sealed class TransferModelTests
{
    [Fact]
    public void Constructor_MutableCollections_CopiesPlanContents()
    {
        var actions = new List<TransferAction>();
        var issues = new List<TransferIssue>();
        var plan = new TransferPlan(actions, issues, "source", "destination", "list", 0);

        actions.Add(TransferAction.Delete("identity", "Person Name TEST", Record()));
        issues.Add(new TransferIssue(
            IssueType.MissingSource, "identity", "Person Name TEST", "detail", []));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Issues);
        Assert.All(plan.ActionCounts.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void Add_ValidArguments_RequiresSourceAndCopiesPdfEntries()
    {
        var entries = new List<PdfEntry>
        {
            new("Person Name TEST", "identity", 1, 1),
        };

        var action = TransferAction.Add("identity", "Person Name TEST", entries, Record());
        entries.Clear();

        Assert.Equal(ActionType.Add, action.ActionType);
        Assert.NotNull(action.Source);
        Assert.Null(action.Destination);
        Assert.Single(action.PdfEntries);
    }

    [Fact]
    public void Add_MissingSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TransferAction.Add("identity", "Person Name TEST", [], null!));
    }

    [Fact]
    public void DocumentSnapshot_MutableRecords_CopiesContentsAndDuplicates()
    {
        var records = new List<DocumentRecord> { Record() };
        var snapshot = new DocumentSnapshot("document.docx", "hash", 0, 0, 1, records, []);

        records.Add(Record() with { RowIndex = 2 });

        Assert.Single(snapshot.Records);
        Assert.Empty(snapshot.Duplicates);
    }

    private static DocumentRecord Record()
    {
        return new DocumentRecord(
            "document.docx",
            0,
            1,
            "Person Name TEST",
            "identity",
            "<w:tr />",
            RecordDependencies.Empty);
    }
}
