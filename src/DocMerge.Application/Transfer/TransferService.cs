using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Validation;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

public sealed class TransferService
{
    private readonly IPdfNameReader _pdfReader;
    private readonly IDocumentReader _documentReader;
    private readonly IDocumentEditor _documentEditor;
    private readonly ITransferResultValidator _validator;
    private readonly IFileStore _fileStore;
    private readonly TransferPlanner _planner;

    public TransferService(
        IPdfNameReader pdfReader,
        IDocumentReader documentReader,
        IDocumentEditor documentEditor,
        ITransferResultValidator validator,
        IFileStore fileStore,
        TransferPlanner planner)
    {
        _pdfReader = pdfReader;
        _documentReader = documentReader;
        _documentEditor = documentEditor;
        _validator = validator;
        _fileStore = fileStore;
        _planner = planner;
    }

    public async Task<TransferPlan> PrepareAsync(
        TransferInputs inputs,
        CancellationToken cancellationToken)
    {
        var pdf = await _pdfReader.ReadAsync(inputs.ListPath, cancellationToken);
        var source = await _documentReader.ReadAsync(
            inputs.SourcePath, inputs.Header, cancellationToken);
        var destination = await _documentReader.ReadAsync(
            inputs.DestinationPath, inputs.Header, cancellationToken);
        return _planner.Create(pdf, source, destination);
    }

    public async Task<OperationResult> ExecuteAsync(
        TransferExecutionRequest request,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OperationProgress(OperationStage.ReadingInputs, 0));
        var plan = request.Plan ?? await PrepareAsync(request.Inputs, cancellationToken);
        var directory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath))!;
        Directory.CreateDirectory(directory);
        var transferred = _fileStore.CreateTemporaryPath(directory, "transferido");
        var sorted = _fileStore.CreateTemporaryPath(directory, "ordenado");
        try
        {
            await GenerateAndValidateAsync(request, plan, transferred, sorted, progress, cancellationToken);
            progress?.Report(new OperationProgress(OperationStage.Publishing, 0));
            await _fileStore.PublishAsync(sorted, request.OutputPath, cancellationToken);
            return new OperationResult(request.OutputPath, plan);
        }
        finally
        {
            _fileStore.DeleteIfExists(transferred);
            _fileStore.DeleteIfExists(sorted);
        }
    }

    private async Task GenerateAndValidateAsync(
        TransferExecutionRequest request,
        TransferPlan plan,
        string transferred,
        string sorted,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OperationProgress(OperationStage.ImportingRows, 0, plan.Actions.Count));
        var applyRequest = new ApplyTransferPlanRequest(
            request.Inputs.SourcePath,
            request.Inputs.DestinationPath,
            transferred,
            plan,
            request.Inputs.Header);
        await _documentEditor.ApplyTransferPlanAsync(applyRequest, cancellationToken);
        progress?.Report(new OperationProgress(OperationStage.Sorting, 0));
        var sortRequest = new SortDocumentRequest(
            transferred, sorted, request.SortingProfile, request.Inputs.Header);
        await _documentEditor.SortAsync(sortRequest, cancellationToken);
        await VerifyInputsAsync(request.Inputs, plan, cancellationToken);
        progress?.Report(new OperationProgress(OperationStage.Validating, 0));
        var validationRequest = new TransferValidationRequest(
            sorted,
            request.Inputs.DestinationPath,
            plan,
            request.SortingProfile,
            request.Inputs.Header);
        await _validator.ValidateAsync(validationRequest, cancellationToken);
    }

    private async Task VerifyInputsAsync(
        TransferInputs inputs,
        TransferPlan plan,
        CancellationToken cancellationToken)
    {
        await VerifyHashAsync(inputs.SourcePath, plan.SourceHash, "de origen", cancellationToken);
        await VerifyHashAsync(inputs.DestinationPath, plan.DestinationHash, "de destino", cancellationToken);
        await VerifyHashAsync(inputs.ListPath, plan.ListHash, "PDF", cancellationToken);
    }

    private async Task VerifyHashAsync(
        string path,
        string expected,
        string description,
        CancellationToken cancellationToken)
    {
        if (expected.Length == 0)
        {
            return;
        }

        var actual = await _fileStore.ComputeHashAsync(path, cancellationToken);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new TransferValidationException(
                $"El archivo {description} cambió durante la operación.");
        }
    }
}
