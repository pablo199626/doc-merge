using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Validation;
using DocMerge.Domain.Transfer;

namespace DocMerge.Application.Transfer;

/// <summary>Coordinates planning, generation, validation, and atomic publication of transfers.</summary>
public sealed class TransferService
{
    private readonly IPdfNameReader _pdfReader;
    private readonly IDocumentReader _documentReader;
    private readonly ITransferPlanEditor _transferEditor;
    private readonly IDocumentSorter _sorter;
    private readonly ITransferResultValidator _validator;
    private readonly IFileStore _fileStore;
    private readonly TransferPlanner _planner;

    /// <summary>Initializes a new instance of the <see cref="TransferService"/> class.</summary>
    public TransferService(
        IPdfNameReader pdfReader,
        IDocumentReader documentReader,
        ITransferPlanEditor transferEditor,
        IDocumentSorter sorter,
        ITransferResultValidator validator,
        IFileStore fileStore,
        TransferPlanner planner)
    {
        _pdfReader = pdfReader ?? throw new ArgumentNullException(nameof(pdfReader));
        _documentReader = documentReader ?? throw new ArgumentNullException(nameof(documentReader));
        _transferEditor = transferEditor ?? throw new ArgumentNullException(nameof(transferEditor));
        _sorter = sorter ?? throw new ArgumentNullException(nameof(sorter));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    /// <summary>Reads the inputs and creates an immutable transfer plan.</summary>
    /// <param name="inputs">The source, destination, PDF, and header inputs.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The prepared transfer plan.</returns>
    public async Task<TransferPlan> PrepareAsync(
        TransferInputs inputs,
        CancellationToken cancellationToken)
    {
        return await PrepareAsync(inputs, null, cancellationToken);
    }

    private async Task<TransferPlan> PrepareAsync(
        TransferInputs inputs,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        OperationRequestValidator.ValidateTransferInputs(inputs);
        progress?.Report(new OperationProgress(OperationStage.ReadingInputs, 0));
        var pdf = await _pdfReader.ReadAsync(inputs.ListPath, cancellationToken);
        var source = await _documentReader.ReadAsync(
            inputs.SourcePath, inputs.Header, cancellationToken);
        var destination = await _documentReader.ReadAsync(
            inputs.DestinationPath, inputs.Header, cancellationToken);
        progress?.Report(new OperationProgress(OperationStage.Planning, 0));
        return _planner.Create(pdf, source, destination);
    }

    /// <summary>Generates, validates, and atomically publishes a transfer result.</summary>
    /// <param name="request">The transfer execution request.</param>
    /// <param name="progress">An optional progress receiver.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The published operation result.</returns>
    public async Task<OperationResult> ExecuteAsync(
        TransferExecutionRequest request,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        OperationRequestValidator.ValidateTransferExecution(request, _fileStore);
        var plan = request.Plan
            ?? await PrepareAsync(request.Inputs, progress, cancellationToken);
        progress?.Report(new OperationProgress(OperationStage.VerifyingInputs, 0));
        await VerifyInputsAsync(request.Inputs, plan, cancellationToken);
        var transferred = _fileStore.CreateTemporaryPath(request.OutputPath, "transferido");
        var sorted = _fileStore.CreateTemporaryPath(request.OutputPath, "ordenado");
        try
        {
            var context = new TransferExecutionContext(
                request, plan, transferred, sorted, progress);
            await GenerateAndValidateAsync(context, cancellationToken);
            progress?.Report(new OperationProgress(OperationStage.Publishing, 0));
            await _fileStore.PublishAsync(sorted, request.OutputPath, cancellationToken);
            progress?.Report(new OperationProgress(OperationStage.Completed, 1, 1));
            return new OperationResult(request.OutputPath, plan);
        }
        finally
        {
            _fileStore.DeleteIfExists(transferred);
            _fileStore.DeleteIfExists(sorted);
        }
    }

    private async Task GenerateAndValidateAsync(
        TransferExecutionContext context,
        CancellationToken cancellationToken)
    {
        var request = context.Request;
        var plan = context.Plan;
        context.Progress?.Report(new OperationProgress(
            OperationStage.ImportingRows, 0, plan.Actions.Count));
        var applyRequest = new ApplyTransferPlanRequest(
            request.Inputs.SourcePath,
            request.Inputs.DestinationPath,
            context.TransferredPath,
            plan,
            request.Inputs.Header);
        await _transferEditor.ApplyTransferPlanAsync(applyRequest, cancellationToken);
        context.Progress?.Report(new OperationProgress(
            OperationStage.ImportingRows, plan.Actions.Count, plan.Actions.Count));
        context.Progress?.Report(new OperationProgress(OperationStage.Sorting, 0));
        var sortRequest = new SortDocumentRequest(
            context.TransferredPath,
            context.SortedPath,
            request.SortingProfile,
            request.Inputs.Header);
        await _sorter.SortAsync(sortRequest, cancellationToken);
        await VerifyInputsAsync(request.Inputs, plan, cancellationToken);
        context.Progress?.Report(new OperationProgress(OperationStage.Validating, 0));
        var validationRequest = new TransferValidationRequest(
            context.SortedPath,
            request.Inputs.SourcePath,
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

    private sealed record TransferExecutionContext(
        TransferExecutionRequest Request,
        TransferPlan Plan,
        string TransferredPath,
        string SortedPath,
        IProgress<OperationProgress>? Progress);
}
