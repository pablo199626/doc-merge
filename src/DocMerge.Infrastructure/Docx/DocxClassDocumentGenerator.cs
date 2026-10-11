using System.Security.Cryptography;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Application.Generation;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxClassDocumentGenerator : IClassDocumentGenerator
{
    private const string UnresolvedFillColor = "FFF2CC";

    public async Task<IReadOnlyList<GeneratedClassDocument>> GenerateAsync(
        GenerateClassDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(request.WorkingDirectory);
        var templateBytes = await ReadAndVerifyAsync(
            request.Prepared.Template.DocumentPath,
            request.Prepared.Template.Sha256,
            cancellationToken);
        var sources = await ReadSourcesAsync(
            request.Prepared.HistoricalDocuments, cancellationToken);
        var generated = new List<GeneratedClassDocument>(request.Prepared.Plan.Classes.Count);
        foreach (var classPlan in request.Prepared.Plan.Classes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputPath = Path.Combine(
                request.WorkingDirectory, classPlan.Roster.ClassIdentifier.OutputStem + ".docx");
            await GenerateClassAsync(
                templateBytes,
                request.Prepared.Template,
                classPlan,
                request.SortingProfile,
                sources,
                outputPath,
                request.Prepared.Request.Header,
                cancellationToken);
            generated.Add(new GeneratedClassDocument(
                classPlan.Roster.ClassIdentifier, outputPath));
        }

        return generated;
    }

    private static async Task<Dictionary<string, DocxPackage>> ReadSourcesAsync(
        IReadOnlyList<HistoricalDocumentSnapshot> documents,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, DocxPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            var content = await ReadAndVerifyAsync(
                document.DocumentPath, document.Sha256, cancellationToken);
            result.Add(Path.GetFullPath(document.DocumentPath), DocxPackage.FromBytes(content));
        }

        return result;
    }

    private static async Task<byte[]> ReadAndVerifyAsync(
        string path,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
        {
            throw new DocumentStructureException(
                $"El archivo cambió después de preparar el plan: {path}");
        }

        return content;
    }

    private static async Task GenerateClassAsync(
        byte[] templateBytes,
        GenerationTemplateSnapshot template,
        ClassDocumentPlan classPlan,
        SortingProfile sortingProfile,
        IReadOnlyDictionary<string, DocxPackage> sources,
        string outputPath,
        string header,
        CancellationToken cancellationToken)
    {
        var destination = DocxPackage.FromBytes(templateBytes);
        var prototype = XElement.Parse(template.PrototypeRowXml, LoadOptions.PreserveWhitespace);
        var orderedPlans = classPlan.Rows.OrderBy(
            row => sortingProfile.CreateKey(row.RosterEntry.OriginalName),
            SortKeyComparer.Instance).ToArray();
        var rows = BuildRows(
            orderedPlans, prototype, template.NameColumnIndex, destination, sources, cancellationToken);
        ReplacePrototype(destination, header, rows);
        await destination.WriteAsync(outputPath, cancellationToken);
    }

    private static IReadOnlyList<XElement> BuildRows(
        IReadOnlyList<StudentRowPlan> plans,
        XElement prototype,
        int nameColumnIndex,
        DocxPackage destination,
        IReadOnlyDictionary<string, DocxPackage> sources,
        CancellationToken cancellationToken)
    {
        var output = new XElement?[plans.Count];
        var historical = plans.Select((plan, index) => (plan, index))
            .Where(item => item.plan.Resolution.SelectedCandidate is not null)
            .GroupBy(item => Path.GetFullPath(
                item.plan.Resolution.SelectedCandidate!.Record.DocumentPath),
                StringComparer.OrdinalIgnoreCase);
        foreach (var group in historical)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var indexedRows = group.Select(item => (
                item.index,
                Row: AdaptHistoricalRow(
                    prototype,
                    item.plan.Resolution.SelectedCandidate!.Record.RowXml,
                    nameColumnIndex,
                    item.plan.RosterEntry.OriginalName))).ToArray();
            var imported = DocxRowImporter.Import(
                sources[group.Key],
                destination,
                indexedRows.Select(item => item.Row).ToArray(),
                cancellationToken);
            for (var index = 0; index < indexedRows.Length; index++)
            {
                output[indexedRows[index].index] = imported[index];
            }
        }

        for (var index = 0; index < plans.Count; index++)
        {
            output[index] ??= CreateUnresolvedRow(
                prototype, nameColumnIndex, plans[index].RosterEntry.OriginalName);
        }

        return output.Select(row => row!).ToArray();
    }

    private static XElement AdaptHistoricalRow(
        XElement prototype,
        string sourceRowXml,
        int nameColumnIndex,
        string pdfName)
    {
        var result = new XElement(prototype);
        var source = XElement.Parse(sourceRowXml, LoadOptions.PreserveWhitespace);
        var resultCells = DocxXml.RowCells(result);
        var sourceCells = DocxXml.RowCells(source);
        if (resultCells.Count != sourceCells.Count)
        {
            throw new DocumentStructureException(
                "Una fila histórica no tiene las mismas columnas que la plantilla.");
        }

        for (var index = 0; index < resultCells.Count; index++)
        {
            ReplaceCellContent(resultCells[index], sourceCells[index]);
        }

        ReplaceVisibleText(resultCells[nameColumnIndex], pdfName);
        return result;
    }

    private static void ReplaceCellContent(XElement destination, XElement source)
    {
        var destinationProperties = destination.Element(DocxNames.Word + "tcPr");
        destination.RemoveNodes();
        if (destinationProperties is not null)
        {
            destination.Add(new XElement(destinationProperties));
        }

        destination.Add(source.Elements()
            .Where(element => element.Name != DocxNames.Word + "tcPr")
            .Select(element => new XElement(element)));
    }

    private static XElement CreateUnresolvedRow(
        XElement prototype,
        int nameColumnIndex,
        string name)
    {
        var row = new XElement(prototype);
        var nameCell = DocxXml.RowCells(row)[nameColumnIndex];
        ReplaceVisibleText(nameCell, name);
        var properties = nameCell.Element(DocxNames.Word + "tcPr");
        if (properties is null)
        {
            properties = new XElement(DocxNames.Word + "tcPr");
            nameCell.AddFirst(properties);
        }

        properties.Add(new XElement(
            DocxNames.Word + "shd",
            new XAttribute(DocxNames.Value, "clear"),
            new XAttribute(DocxNames.Word + "color", "auto"),
            new XAttribute(DocxNames.Word + "fill", UnresolvedFillColor)));
        return row;
    }

    private static void ReplaceVisibleText(XElement cell, string value)
    {
        var textNodes = cell.Descendants(DocxNames.Word + "t").ToArray();
        if (textNodes.Length == 0)
        {
            cell.Add(new XElement(
                DocxNames.Word + "p",
                new XElement(
                    DocxNames.Word + "r",
                    new XElement(DocxNames.Word + "t", value))));
            return;
        }

        textNodes[0].Value = value;
        foreach (var extra in textNodes.Skip(1))
        {
            extra.Value = string.Empty;
        }
    }

    private static void ReplacePrototype(
        DocxPackage package,
        string header,
        IReadOnlyList<XElement> rows)
    {
        var document = DocxXml.Parse(package.Parts[DocxNames.DocumentPart]);
        var location = DocxXml.LocateStrictTable(document, header);
        foreach (var row in location.Table.Elements(DocxNames.Word + "tr")
            .Skip(location.HeaderIndex + 1).ToArray())
        {
            row.Remove();
        }

        location.Table.Add(rows);
        package.Parts[DocxNames.DocumentPart] = DocxXml.Serialize(document);
    }
}
