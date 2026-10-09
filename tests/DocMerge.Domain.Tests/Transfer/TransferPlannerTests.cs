using System.Text.Json;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Identity;
using DocMerge.Domain.Pdf;
using DocMerge.Domain.Transfer;

namespace DocMerge.Domain.Tests.Transfer;

public sealed class TransferPlannerTests
{
    private readonly TransferPlanner _planner = new();

    [Fact]
    public void Create_AllPythonAcceptanceCases_ProducesExpectedActions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "casos.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var testCase in json.RootElement.GetProperty("casos").EnumerateArray())
        {
            AssertCase(testCase);
        }
    }

    [Fact]
    public void Create_AmbiguousDestinations_OrdersIssueByDocumentAppearance()
    {
        var destination = Snapshot([
            Record("Second Key ANA", "d1", 1),
            Record("First Key ANA", "d2", 2),
            Record("Second Key ANA", "d3", 3),
            Record("First Key ANA", "d4", 4),
        ], "destination");

        var plan = _planner.Create(Pdf([]), Snapshot([], "source"), destination);

        Assert.Equal(
            ["Second Key ANA", "First Key ANA"],
            plan.Issues.Select(issue => issue.Name));
    }

    private void AssertCase(JsonElement testCase)
    {
        var sources = testCase.GetProperty("origen").EnumerateArray().ToArray();
        var destinations = testCase.GetProperty("destino").EnumerateArray().ToArray();
        var source = Snapshot(CreateRecords(sources, "source"), "source");
        var destination = Snapshot(CreateRecords(destinations, "destination"), "destination");
        var names = testCase.GetProperty("pdf").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        var actual = _planner.Create(Pdf(names), source, destination).Actions;
        var expected = testCase.GetProperty("acciones").EnumerateArray().ToArray();

        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            AssertAction(expected[index], actual[index], sources, destinations);
        }
    }

    private static IReadOnlyList<DocumentRecord> CreateRecords(
        IReadOnlyList<JsonElement> elements,
        string path)
    {
        return elements.Select((element, index) => Record(
            element.GetProperty("nombre").GetString()!,
            element.GetProperty("id").GetString()!,
            index + 1,
            path)).ToArray();
    }

    private static DocumentRecord Record(
        string name,
        string marker,
        int index,
        string path = "destination")
    {
        return new DocumentRecord(
            path, 0, index, name, IdentityNormalizer.Normalize(name), marker,
            RecordDependencies.Empty);
    }

    private static DocumentSnapshot Snapshot(
        IReadOnlyList<DocumentRecord> records,
        string hash)
    {
        return new DocumentSnapshot(hash, hash, 0, 0, 1, records, []);
    }

    private static PdfReadResult Pdf(IReadOnlyList<string> names)
    {
        var entries = names.Select((name, index) => new PdfEntry(
            name, IdentityNormalizer.Normalize(name), 1, index + 1)).ToArray();
        return new PdfReadResult(entries, entries.Length, 1);
    }

    private static void AssertAction(
        JsonElement expected,
        TransferAction actual,
        IReadOnlyList<JsonElement> sources,
        IReadOnlyList<JsonElement> destinations)
    {
        Assert.Equal(ParseAction(expected.GetProperty("tipo").GetString()!), actual.ActionType);
        AssertReference(expected, "origen", actual.Source, sources);
        AssertReference(expected, "destino", actual.Destination, destinations);
        if (expected.TryGetProperty("motivo", out var reason))
        {
            Assert.Equal(ParseIssue(reason.GetString()!), actual.Reason);
        }
    }

    private static void AssertReference(
        JsonElement expected,
        string property,
        DocumentRecord? actual,
        IReadOnlyList<JsonElement> records)
    {
        if (!expected.TryGetProperty(property, out var expectedId))
        {
            Assert.Null(actual);
            return;
        }

        var actualId = records[actual!.RowIndex - 1].GetProperty("id").GetString();
        Assert.Equal(expectedId.GetString(), actualId);
    }

    private static ActionType ParseAction(string value) => value switch
    {
        "anadir" => ActionType.Add,
        "reemplazar" => ActionType.Replace,
        "conservar" => ActionType.Keep,
        "eliminar" => ActionType.Delete,
        "omitir" => ActionType.Skip,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static IssueType ParseIssue(string value) => value switch
    {
        "pdf_duplicado" => IssueType.DuplicatePdfEntry,
        "destino_ambiguo" => IssueType.AmbiguousDestination,
        "origen_ausente" => IssueType.MissingSource,
        "origen_ambiguo" => IssueType.AmbiguousSource,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
