using DocMerge.Domain.Documents;
using DocMerge.Domain.Pdf;

namespace DocMerge.Domain.Transfer;

/// <summary>Builds a deterministic transfer plan from validated input snapshots.</summary>
public sealed class TransferPlanner
{
    /// <summary>Creates the actions and issues required to reconcile the destination with the PDF list.</summary>
    /// <param name="pdf">The validated PDF list.</param>
    /// <param name="source">The source document snapshot.</param>
    /// <param name="destination">The destination document snapshot.</param>
    /// <returns>An immutable transfer plan.</returns>
    public TransferPlan Create(
        PdfReadResult pdf,
        DocumentSnapshot source,
        DocumentSnapshot destination)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var groups = new PlanningGroups(
            GroupPdfEntries(pdf.Entries),
            GroupRecords(source.Records),
            GroupRecords(destination.Records),
            FindAmbiguousDestinationKeys(destination));
        var issues = CreateDuplicatePdfIssues(groups.Pdf);
        var actions = PreserveAmbiguousDestinations(destination, groups, issues);
        ResolveListedPeople(groups, actions, issues);
        AddDeletions(destination, groups, actions);
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
        PlanningGroups groups,
        ICollection<TransferIssue> issues)
    {
        var actions = destination.Records.Where(
            record => groups.AmbiguousDestinationKeys.Contains(record.IdentityKey))
            .Select(record => TransferAction.Keep(
                record.IdentityKey,
                record.OriginalName,
                groups.Pdf.GetValueOrDefault(record.IdentityKey) ?? [],
                record,
                IssueType.AmbiguousDestination))
            .ToList();
        foreach (var key in OrderedAmbiguousKeys(destination, groups.AmbiguousDestinationKeys))
        {
            issues.Add(CreateDestinationIssue(key, groups.Destination[key]));
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
        PlanningGroups groups,
        ICollection<TransferAction> actions,
        ICollection<TransferIssue> issues)
    {
        foreach (var pair in groups.Pdf)
        {
            if (groups.AmbiguousDestinationKeys.Contains(pair.Key))
            {
                continue;
            }

            ResolveIdentity(pair, groups, actions, issues);
        }
    }

    private static void ResolveIdentity(
        KeyValuePair<string, List<PdfEntry>> pdfGroup,
        PlanningGroups groups,
        ICollection<TransferAction> actions,
        ICollection<TransferIssue> issues)
    {
        var sources = groups.Source.GetValueOrDefault(pdfGroup.Key) ?? [];
        var destinations = groups.Destination.GetValueOrDefault(pdfGroup.Key) ?? [];
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
        if (destination is null)
        {
            return TransferAction.Add(
                pdfGroup.Key, pdfGroup.Value[0].OriginalText, pdfGroup.Value, source);
        }

        return TransferAction.Replace(
            pdfGroup.Key, pdfGroup.Value[0].OriginalText, pdfGroup.Value, source, destination);
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
        var action = destination is null
            ? TransferAction.Skip(
                pdfGroup.Key, pdfGroup.Value[0].OriginalText, pdfGroup.Value, issueType)
            : TransferAction.Keep(
                pdfGroup.Key, pdfGroup.Value[0].OriginalText, pdfGroup.Value, destination, issueType);
        var issue = new TransferIssue(
            issueType, pdfGroup.Key, pdfGroup.Value[0].OriginalText, detail, references);
        return new UnsafeResolution(action, issue);
    }

    private static void AddDeletions(
        DocumentSnapshot destination,
        PlanningGroups groups,
        ICollection<TransferAction> actions)
    {
        foreach (var record in destination.Records)
        {
            if (groups.Pdf.ContainsKey(record.IdentityKey)
                || groups.AmbiguousDestinationKeys.Contains(record.IdentityKey))
            {
                continue;
            }

            actions.Add(TransferAction.Delete(record.IdentityKey, record.OriginalName, record));
        }
    }

    private sealed record UnsafeResolution(TransferAction Action, TransferIssue Issue);

    private sealed record PlanningGroups(
        IReadOnlyDictionary<string, List<PdfEntry>> Pdf,
        IReadOnlyDictionary<string, List<DocumentRecord>> Source,
        IReadOnlyDictionary<string, List<DocumentRecord>> Destination,
        IReadOnlySet<string> AmbiguousDestinationKeys);
}
