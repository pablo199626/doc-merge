namespace DocMerge.Application.Documents;

/// <summary>Describes a verified working copy of a template embedded in DocMerge.</summary>
public sealed record BundledGenerationTemplate(
    string TemplateId,
    int TemplateVersion,
    string DocumentFormatId,
    int HeaderContractVersion,
    string DisplayName,
    string Sha256,
    string Path);
