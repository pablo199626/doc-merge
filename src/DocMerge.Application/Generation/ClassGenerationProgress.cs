namespace DocMerge.Application.Generation;

/// <summary>Identifies a measurable stage of class generation.</summary>
public enum ClassGenerationStage
{
    /// <summary>Input files are being discovered.</summary>
    DiscoveringInputs,
    /// <summary>The template is being read.</summary>
    ReadingTemplate,
    /// <summary>Class rosters are being read.</summary>
    ReadingRosters,
    /// <summary>Historical documents are being read.</summary>
    ReadingHistoricalDocuments,
    /// <summary>The generation plan is being created.</summary>
    Planning,
    /// <summary>The reviewed inputs are being verified.</summary>
    VerifyingInputs,
    /// <summary>Class documents are being generated.</summary>
    GeneratingDocuments,
    /// <summary>Generated documents are being validated.</summary>
    ValidatingDocuments,
    /// <summary>The validated batch is being published.</summary>
    Publishing,
    /// <summary>The operation completed successfully.</summary>
    Completed,
}

/// <summary>Reports generation progress without depending on a presentation technology.</summary>
/// <param name="Stage">The current operation stage.</param>
/// <param name="Completed">The number of completed units.</param>
/// <param name="Total">The total units, when known.</param>
public sealed record ClassGenerationProgress(
    ClassGenerationStage Stage,
    int Completed = 0,
    int? Total = null);
