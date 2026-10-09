using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;

namespace DocMerge.Domain.Transfer;

public sealed class TransferPlanner
{
    public TransferPlan Create(
        PdfReadResult pdf,
        DocumentSnapshot source,
        DocumentSnapshot destination)
    {
        var pdfGroups = GroupPdfEntries(pdf.Entries);
        var sourceGroups = GroupRecords(source.Records);
        var destinationGroups = GroupRecords(destination.Records);
        var issues = CreateDuplicatePdfIssues(pdfGroups);
        var ambiguousKeys = FindAmbiguousDestinationKeys(destination);
        var actions = PreserveAmbiguousDestinations(
            destination, destinationGroups, pdfGroups, ambiguousKeys, issues);
        ResolveListedPeople(pdfGroups, sourceGroups, destinationGroups, ambiguousKeys, actions, issues);
        AddDeletions(destination, pdfGroups, ambiguousKeys, actions);
        return new TransferPlan(
            actions, issues, source.Sha256, destination.Sha256, pdf.Sha256, pdf.DeclaredTotal);
    }

    private static Dictionary<string, List<PdfEntry>> GroupPdfEntries(
        IReadOnlyList<PdfEntry> entries)
    {
        var groups = new Dictionary<string, List<PdfEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!groups.TryGetValue(entry.IdentityKey, out var group))
            {
                group = [];
                groups.Add(entry.IdentityKey, group);
            }

            group.Add(entry);
        }

        return groups;
    }

    private static Dictionary<string, List<DocumentRecord>> GroupRecords(
        IReadOnlyList<DocumentRecord> records)
    {
        return records.GroupBy(record => record.IdentityKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    private static List<TransferIssue> CreateDuplicatePdfIssues(
        IReadOnlyDictionary<string, List<PdfEntry>> groups)
    {
        return groups.Where(pair => pair.Value.Count > 1)
            .Select(pair => new TransferIssue(
                IssueType.DuplicatePdfEntry,
                pair.Key,
                pair.Value[0].OriginalText,
                $"El PDF contiene {pair.Value.Count} entradas para la misma persona.",
                pair.Value.Select(entry => entry.Location).ToArray()))
            .ToList();
    }

    private static HashSet<string> FindAmbiguousDestinationKeys(DocumentSnapshot destination)
    {
        return destination.Records
            .GroupBy(record => record.IdentityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static List<TransferAction> PreserveAmbiguousDestinations(
        DocumentSnapshot destination,
        IReadOnlyDictionary<string, List<DocumentRecord>> destinationGroups,
        IReadOnlyDictionary<string, List<PdfEntry>> pdfGroups,
        IReadOnlySet<string> ambiguousKeys,
        ICollection<TransferIssue> issues)
    {
        var actions = destination.Records.Where(record => ambiguousKeys.Contains(record.IdentityKey))
            .Select(record => new TransferAction(
                ActionType.Keep,
                record.IdentityKey,
                record.OriginalName,
                pdfGroups.GetValueOrDefault(record.IdentityKey) ?? [],
                Destination: record,
                Reason: IssueType.AmbiguousDestination))
            .ToList();
        foreach (var key in OrderedAmbiguousKeys(destination, ambiguousKeys))
        {
            issues.Add(CreateDestinationIssue(key, destinationGroups[key]));
        }

        return actions;
    }

    private static IEnumerable<string> OrderedAmbiguousKeys(
        DocumentSnapshot destination,
        IReadOnlySet<string> ambiguousKeys)
    {
        return destination.Records.Select(record => record.IdentityKey)
            .Where(ambiguousKeys.Contains)
            .Distinct(StringComparer.Ordinal);
    }

    private static TransferIssue CreateDestinationIssue(
        string identityKey,
        IReadOnlyList<DocumentRecord> records)
    {
        return new TransferIssue(
            IssueType.AmbiguousDestination,
            identityKey,
            records[0].OriginalName,
            $"El destino contiene {records.Count} registros con la misma identidad; se conservan todos.",
            records.Select(record => record.Location).ToArray());
    }

    private static void ResolveListedPeople(
        IReadOnlyDictionary<string, List<PdfEntry>> pdfGroups,
        IReadOnlyDictionary<string, List<DocumentRecord>> sourceGroups,
        IReadOnlyDictionary<string, List<DocumentRecord>> destinationGroups,
        IReadOnlySet<string> ambiguousKeys,
        ICollection<TransferAction> actions,
        ICollection<TransferIssue> issues)
    {
        foreach (var pair in pdfGroups)
        {
            if (ambiguousKeys.Contains(pair.Key))
            {
                continue;
            }

            ResolveIdentity(pair, sourceGroups, destinationGroups, actions, issues);
        }
    }

    private static void ResolveIdentity(
        KeyValuePair<string, List<PdfEntry>> pdfGroup,
        IReadOnlyDictionary<string, List<DocumentRecord>> sourceGroups,
        IReadOnlyDictionary<string, List<DocumentRecord>> destinationGroups,
        ICollection<TransferAction> actions,
        ICollection<TransferIssue> issues)
    {
        var sources = sourceGroups.GetValueOrDefault(pdfGroup.Key) ?? [];
        var destinations = destinationGroups.GetValueOrDefault(pdfGroup.Key) ?? [];
        if (sources.Count == 1)
        {
            actions.Add(CreateSourceAction(pdfGroup, sources[0], destinations.FirstOrDefault()));
            return;
        }

        var resolution = ResolveUnsafeSource(pdfGroup, sources, destinations.FirstOrDefault());
        actions.Add(resolution.Action);
        issues.Add(resolution.Issue);
    }

    private static TransferAction CreateSourceAction(
        KeyValuePair<string, List<PdfEntry>> pdfGroup,
        DocumentRecord source,
        DocumentRecord? destination)
    {
        return new TransferAction(
            destination is null ? ActionType.Add : ActionType.Replace,
            pdfGroup.Key,
            pdfGroup.Value[0].OriginalText,
            pdfGroup.Value,
            source,
            destination);
    }

    private static UnsafeResolution ResolveUnsafeSource(
        KeyValuePair<string, List<PdfEntry>> pdfGroup,
        IReadOnlyList<DocumentRecord> sources,
        DocumentRecord? destination)
    {
        var issueType = sources.Count == 0 ? IssueType.MissingSource : IssueType.AmbiguousSource;
        var detail = sources.Count == 0
            ? "La persona del PDF no aparece en el documento de origen."
            : $"El origen contiene {sources.Count} registros con la misma identidad.";
        var references = pdfGroup.Value.Select(entry => entry.Location)
            .Concat(sources.Select(record => record.Location)).ToArray();
        var action = new TransferAction(
            destination is null ? ActionType.Skip : ActionType.Keep,
            pdfGroup.Key,
            pdfGroup.Value[0].OriginalText,
            pdfGroup.Value,
            Destination: destination,
            Reason: issueType);
        var issue = new TransferIssue(
            issueType, pdfGroup.Key, pdfGroup.Value[0].OriginalText, detail, references);
        return new UnsafeResolution(action, issue);
    }

    private static void AddDeletions(
        DocumentSnapshot destination,
        IReadOnlyDictionary<string, List<PdfEntry>> pdfGroups,
        IReadOnlySet<string> ambiguousKeys,
        ICollection<TransferAction> actions)
    {
        foreach (var record in destination.Records)
        {
            if (pdfGroups.ContainsKey(record.IdentityKey) || ambiguousKeys.Contains(record.IdentityKey))
            {
                continue;
            }

            actions.Add(new TransferAction(
                ActionType.Delete, record.IdentityKey, record.OriginalName, [], Destination: record));
        }
    }

    private sealed record UnsafeResolution(TransferAction Action, TransferIssue Issue);
}
