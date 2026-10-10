namespace DocMerge.Application.Transfer;

/// <summary>Identifies the three inputs required to prepare a transfer.</summary>
/// <param name="SourcePath">The source DOCX path.</param>
/// <param name="DestinationPath">The destination DOCX path.</param>
/// <param name="ListPath">The PDF list path.</param>
/// <param name="Header">The exact identity-column header.</param>
public sealed record TransferInputs(
    string SourcePath,
    string DestinationPath,
    string ListPath,
    string Header = "Nombre y Apellidos");
