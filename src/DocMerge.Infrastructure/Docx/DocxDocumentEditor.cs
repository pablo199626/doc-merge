using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Domain.Sorting;
using DocMerge.Domain.Transfer;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxDocumentEditor : IDocumentEditor
{
    public async Task<int> SortAsync(
        SortDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllBytesAsync(request.InputPath, cancellationToken);
        var package = DocxPackage.FromBytes(content);
        var documentPart = package.Parts.GetValueOrDefault(DocxNames.DocumentPart)
            ?? throw new DocumentStructureException("El archivo no contiene word/document.xml.");
        var document = DocxXml.Parse(documentPart);
        var location = DocxXml.LocateStrictTable(document, request.Header);
        var count = ReorderRows(
            location.Table, location.HeaderIndex, location.NameColumnIndex, request.SortingProfile);
        package.Parts[DocxNames.DocumentPart] = DocxXml.Serialize(document);
        await package.WriteAsync(request.OutputPath, cancellationToken);
        return count;
    }

    public Task<int> ApplyTransferPlanAsync(
        ApplyTransferPlanRequest request,
        CancellationToken cancellationToken)
    {
        var importer = new DocxTransferEditor();
        return importer.ApplyAsync(request, cancellationToken);
    }

    private static int ReorderRows(
        XElement table,
        int headerIndex,
        int nameColumnIndex,
        SortingProfile profile)
    {
        var rows = table.Elements(DocxNames.Word + "tr").ToArray();
        var people = CollectPeople(rows, headerIndex, nameColumnIndex);
        if (people.Count == 0)
        {
            throw new DocumentStructureException(
                "La tabla encontrada no contiene filas de personas para ordenar.");
        }

        var sorted = people.OrderBy(item => profile.CreateKey(item.Name), SortKeyComparer.Instance)
            .Select(item => new XElement(item.Row)).ToArray();
        for (var index = 0; index < people.Count; index++)
        {
            people[index].Row.ReplaceWith(sorted[index]);
        }

        return people.Count;
    }

    private static List<PersonRow> CollectPeople(
        IReadOnlyList<XElement> rows,
        int headerIndex,
        int nameColumnIndex)
    {
        var result = new List<PersonRow>();
        foreach (var row in rows.Skip(headerIndex + 1))
        {
            var cells = DocxXml.RowCells(row);
            if (cells.Count <= nameColumnIndex)
            {
                continue;
            }

            var name = DocxXml.ElementText(cells[nameColumnIndex]).Trim();
            if (name.Length > 0)
            {
                result.Add(new PersonRow(row, name));
            }
        }

        return result;
    }

    private sealed record PersonRow(XElement Row, string Name);
}
