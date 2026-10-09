namespace DocMerge.Application.Transfer;

public sealed record TransferInputs(
    string SourcePath,
    string DestinationPath,
    string ListPath,
    string Header = "Nombre y Apellidos");
