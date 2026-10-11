using DocMerge.Domain.Sorting;

namespace DocMerge.Console.Cli;

public sealed record CliOptions(
    string? Source = null,
    string? Output = null,
    string? Destination = null,
    string? PdfList = null,
    bool Simulate = false,
    string SortRule = SortingProfiles.DefaultIdentifier,
    string Header = "Nombre y Apellidos",
    bool Force = false,
    bool ShowHelp = false,
    bool ShowVersion = false,
    bool IsInteractive = false,
    bool GenerateClasses = false,
    string? HistoricalDirectory = null,
    string? RosterDirectory = null,
    string? TemplatePath = null,
    string? OutputDirectory = null);
