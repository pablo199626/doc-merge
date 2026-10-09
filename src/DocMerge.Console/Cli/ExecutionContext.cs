using DocMerge.Application.Transfer;

namespace DocMerge.Console.Cli;

internal sealed record ExecutionContext(
    CliOptions Options,
    string SourcePath,
    string OutputPath,
    TransferInputs? TransferInputs)
{
    internal bool IsTransfer => TransferInputs is not null;
}
