namespace DocMerge.Application.Documents;

/// <summary>Defines the supported document-header fields in generation templates.</summary>
public static class TemplateHeaderContract
{
    /// <summary>The current header-field contract version.</summary>
    public const int Version = 1;

    /// <summary>The content-control tag for the visible class title.</summary>
    public const string ClassTitleTag = "DocMerge.ClassTitle";

    /// <summary>The content-control tag for the optional tutor name.</summary>
    public const string TutorNameTag = "DocMerge.TutorName";
}
