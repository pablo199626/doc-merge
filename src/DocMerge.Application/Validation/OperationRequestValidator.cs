using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Transfer;

namespace DocMerge.Application.Validation;

internal static class OperationRequestValidator
{
    private const string DocxExtension = ".docx";
    private const string PdfExtension = ".pdf";

    internal static void ValidateSort(
        SortDocumentRequest request,
        IFileStore fileStore)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fileStore);
        ValidateRequiredPath(request.InputPath, "La ruta del documento de entrada es obligatoria.");
        ValidateRequiredPath(request.OutputPath, "La ruta del documento de salida es obligatoria.");
        ValidateExtension(request.InputPath, DocxExtension, "El documento de entrada debe tener extensión .docx.");
        ValidateExtension(request.OutputPath, DocxExtension, "El documento de salida debe tener extensión .docx.");
        ValidateHeader(request.Header);
        ArgumentNullException.ThrowIfNull(request.SortingProfile);
        ValidateDistinctOutput(request.OutputPath, [request.InputPath], fileStore);
    }

    internal static void ValidateTransferInputs(TransferInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ValidateRequiredPath(inputs.SourcePath, "La ruta del documento de origen es obligatoria.");
        ValidateRequiredPath(inputs.DestinationPath, "La ruta del documento de destino es obligatoria.");
        ValidateRequiredPath(inputs.ListPath, "La ruta del listado PDF es obligatoria.");
        ValidateExtension(inputs.SourcePath, DocxExtension, "El documento de origen debe tener extensión .docx.");
        ValidateExtension(inputs.DestinationPath, DocxExtension, "El documento de destino debe tener extensión .docx.");
        ValidateExtension(inputs.ListPath, PdfExtension, "El listado debe tener extensión .pdf.");
        ValidateHeader(inputs.Header);
    }

    internal static void ValidateTransferExecution(
        TransferExecutionRequest request,
        IFileStore fileStore)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fileStore);
        ValidateTransferInputs(request.Inputs);
        ValidateRequiredPath(request.OutputPath, "La ruta del documento de salida es obligatoria.");
        ValidateExtension(request.OutputPath, DocxExtension, "El documento de salida debe tener extensión .docx.");
        ArgumentNullException.ThrowIfNull(request.SortingProfile);
        ValidateDistinctOutput(
            request.OutputPath,
            [request.Inputs.SourcePath, request.Inputs.DestinationPath, request.Inputs.ListPath],
            fileStore);
    }

    private static void ValidateRequiredPath(string path, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new OperationValidationException(message);
        }
    }

    private static void ValidateExtension(string path, string extension, string message)
    {
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationValidationException(message);
        }
    }

    private static void ValidateHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new OperationValidationException("La cabecera de identidad es obligatoria.");
        }
    }

    private static void ValidateDistinctOutput(
        string outputPath,
        IReadOnlyList<string> inputPaths,
        IFileStore fileStore)
    {
        var output = fileStore.GetFullPath(outputPath);
        if (inputPaths.Any(input => string.Equals(
            fileStore.GetFullPath(input), output, StringComparison.OrdinalIgnoreCase)))
        {
            throw new OperationValidationException(
                "El archivo de salida debe ser distinto de todos los archivos de entrada.");
        }
    }
}
