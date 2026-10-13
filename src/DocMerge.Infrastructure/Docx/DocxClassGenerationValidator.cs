using System.IO.Compression;
using System.Xml.Linq;
using DocMerge.Application.Documents;
using DocMerge.Application.Generation;
using DocMerge.Application.Validation;
using DocMerge.Domain.Documents;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Sorting;

namespace DocMerge.Infrastructure.Docx;

public sealed class DocxClassGenerationValidator : IClassGenerationValidator
{
    private static readonly HashSet<string> AllowedChangedTemplateParts =
    [
        DocxNames.DocumentPart,
        DocxNames.StylesPart,
        DocxNames.NumberingPart,
        DocxNames.RelationshipsPart,
        DocxNames.ContentTypesPart,
    ];

    private readonly IDocumentReader _reader;

    public DocxClassGenerationValidator(IDocumentReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task ValidateAsync(
        ValidateClassDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var generated = request.GeneratedDocuments.ToDictionary(
            item => item.ClassIdentifier,
            item => item.Path);
        var outputClasses = request.Prepared.Plan.OutputClasses;
        if (generated.Count != outputClasses.Count)
        {
            throw new TransferValidationException(
                "El lote generado no contiene exactamente un documento por clase con alumnado incluido.");
        }

        var templateParts = await ReadPartsAsync(
            request.Prepared.Template.DocumentPath, cancellationToken);
        foreach (var classPlan in outputClasses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!generated.TryGetValue(classPlan.Roster.ClassIdentifier, out var path))
            {
                throw new TransferValidationException(
                    $"Falta el documento de la clase {classPlan.Roster.ClassIdentifier.CanonicalCode}.");
            }

            await ValidateClassAsync(
                path, classPlan, request, templateParts, cancellationToken);
        }
    }

    private async Task ValidateClassAsync(
        string path,
        ClassDocumentPlan classPlan,
        ValidateClassDocumentsRequest request,
        IReadOnlyDictionary<string, byte[]> templateParts,
        CancellationToken cancellationToken)
    {
        var snapshot = await _reader.ReadAsync(
            path, request.Prepared.Request.Header, cancellationToken);
        var expected = classPlan.IncludedRows.OrderBy(
            row => request.SortingProfile.CreateKey(row.RosterEntry.OriginalName),
            SortKeyComparer.Instance).ToArray();
        if (snapshot.Records.Count != expected.Length)
        {
            throw new TransferValidationException(
                $"La clase {classPlan.Roster.ClassIdentifier.CanonicalCode} contiene " +
                $"{snapshot.Records.Count} alumnos; se esperaban {expected.Length}.");
        }

        for (var index = 0; index < expected.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRow(snapshot.Records[index], expected[index], snapshot.NameColumnIndex);
        }

        var resultParts = await ReadPartsAsync(path, cancellationToken);
        ValidateTemplateParts(templateParts, resultParts, cancellationToken);
        ValidateDocumentHeader(
            classPlan, request, templateParts, resultParts);
        ValidateOutsideTargetTable(
            templateParts,
            resultParts,
            request.Prepared.Request.Header,
            request.OutputOptions is not null);
    }

    private static void ValidateRow(
        DocumentRecord actual,
        StudentRowPlan expected,
        int nameColumnIndex)
    {
        if (!string.Equals(
            actual.IdentityKey,
            expected.RosterEntry.IdentityKey,
            StringComparison.Ordinal))
        {
            throw new TransferValidationException(
                $"La identidad generada no coincide con el PDF: {actual.OriginalName}.");
        }

        var actualRow = XElement.Parse(actual.RowXml, LoadOptions.PreserveWhitespace);
        if (expected.Resolution.SelectedCandidate is null)
        {
            ValidateNewStudentRow(actualRow, nameColumnIndex, actual.OriginalName);
            return;
        }

        var sourceRow = XElement.Parse(
            expected.Resolution.SelectedCandidate.Record.RowXml,
            LoadOptions.PreserveWhitespace);
        var actualInformation = InformationCellTexts(actualRow, nameColumnIndex);
        var sourceInformation = InformationCellTexts(sourceRow, nameColumnIndex);
        if (!actualInformation.SequenceEqual(sourceInformation, StringComparer.Ordinal))
        {
            throw new TransferValidationException(
                $"La información generada de {actual.OriginalName} no coincide con su histórico.");
        }
    }

    private static IReadOnlyList<string> InformationCellTexts(XElement row, int nameColumnIndex)
    {
        return DocxXml.RowCells(row).Select((cell, index) => (cell, index))
            .Where(item => item.index != nameColumnIndex)
            .Select(item => DocxXml.ElementText(item.cell))
            .ToArray();
    }

    private static void ValidateNewStudentRow(
        XElement row,
        int nameColumnIndex,
        string studentName)
    {
        var cells = DocxXml.RowCells(row);
        var information = cells.Select((cell, index) => (cell, index))
            .Where(item => item.index != nameColumnIndex)
            .Select(item => DocxXml.ElementText(item.cell));
        if (information.Any(text => text.Length > 0))
        {
            throw new TransferValidationException(
                $"El alumno nuevo {studentName} contiene información inventada.");
        }

    }

    private static void ValidateTemplateParts(
        IReadOnlyDictionary<string, byte[]> template,
        IReadOnlyDictionary<string, byte[]> result,
        CancellationToken cancellationToken)
    {
        foreach (var part in template)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.TryGetValue(part.Key, out var content))
            {
                throw new TransferValidationException(
                    $"El documento generado perdió una parte de la plantilla: {part.Key}.");
            }

            if (!AllowedChangedTemplateParts.Contains(part.Key)
                && !part.Value.SequenceEqual(content))
            {
                throw new TransferValidationException(
                    $"Se modificó una parte no autorizada de la plantilla: {part.Key}.");
            }
        }
    }

    private static void ValidateOutsideTargetTable(
        IReadOnlyDictionary<string, byte[]> template,
        IReadOnlyDictionary<string, byte[]> result,
        string header,
        bool hasPersonalizedHeader)
    {
        var templateDocument = DocxXml.Parse(template[DocxNames.DocumentPart]);
        var resultDocument = DocxXml.Parse(result[DocxNames.DocumentPart]);
        if (hasPersonalizedHeader)
        {
            DocxTemplateHeaderFields.ClearValuesForComparison(templateDocument, header);
            DocxTemplateHeaderFields.ClearValuesForComparison(resultDocument, header);
        }

        DocxXml.LocateStrictTable(templateDocument, header).Table.Remove();
        DocxXml.LocateStrictTable(resultDocument, header).Table.Remove();
        if (!XNode.DeepEquals(templateDocument, resultDocument))
        {
            throw new TransferValidationException(
                "Se modificó contenido situado fuera de la tabla de alumnado.");
        }
    }

    private static void ValidateDocumentHeader(
        ClassDocumentPlan classPlan,
        ValidateClassDocumentsRequest request,
        IReadOnlyDictionary<string, byte[]> template,
        IReadOnlyDictionary<string, byte[]> result)
    {
        if (request.OutputOptions is null)
        {
            return;
        }

        if (!ReferenceEquals(request.OutputOptions.Prepared, request.Prepared)
            || !request.OutputOptions.Headers.TryGetValue(
                classPlan.Roster.ClassIdentifier, out var expected))
        {
            throw new TransferValidationException(
                "Las cabeceras no corresponden con el lote validado.");
        }

        var header = request.Prepared.Request.Header;
        var templateDocument = DocxXml.Parse(template[DocxNames.DocumentPart]);
        var resultDocument = DocxXml.Parse(result[DocxNames.DocumentPart]);
        DocxTemplateHeaderFields.LocateStrict(templateDocument, header);
        var actual = DocxTemplateHeaderFields.LocateStrict(resultDocument, header);
        if (!string.Equals(
                DocxTemplateHeaderFields.ReadValue(actual.ClassTitle),
                expected.DisplayTitle,
                StringComparison.Ordinal)
            || !string.Equals(
                DocxTemplateHeaderFields.ReadValue(actual.TutorName),
                expected.TutorName,
                StringComparison.Ordinal))
        {
            throw new TransferValidationException(
                $"La cabecera generada de {classPlan.Roster.ClassIdentifier.CanonicalCode} no coincide con los valores confirmados.");
        }
    }

    private static async Task<Dictionary<string, byte[]>> ReadPartsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            parts.Add(entry.FullName, buffer.ToArray());
        }

        return parts;
    }
}
