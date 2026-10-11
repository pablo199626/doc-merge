using DocMerge.Application.Generation;
using DocMerge.Application.Transfer;

namespace DocMerge.Console.Cli;

internal sealed record ExecutionContext(
    CliOptions Options,
    string SourcePath,
    string OutputPath,
    TransferInputs? TransferInputs,
    ClassGenerationRequest? GenerationRequest = null);
