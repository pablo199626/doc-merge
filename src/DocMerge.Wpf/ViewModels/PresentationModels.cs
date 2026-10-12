using DocMerge.Application.Generation;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.Services;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Describes a selectable appearance option.</summary>
public sealed record ThemeOptionViewModel(ThemePreference Value, string Label);

/// <summary>Identifies the visual state of a workflow step.</summary>
public enum WorkflowStepState
{
    /// <summary>The step has not been reached.</summary>
    Pending,

    /// <summary>The step is currently active.</summary>
    Current,

    /// <summary>The step has completed successfully.</summary>
    Completed,
}

/// <summary>Identifies one exclusive state of the desktop generation session.</summary>
public enum DesktopWorkflowState
{
    /// <summary>The bundled template is being prepared.</summary>
    Initializing,
    /// <summary>The user can select generation inputs.</summary>
    Selecting,
    /// <summary>The selected inputs are being analyzed.</summary>
    Analyzing,
    /// <summary>An analysis cancellation is awaiting completion.</summary>
    CancellingAnalysis,
    /// <summary>The prepared batch is being reviewed.</summary>
    Reviewing,
    /// <summary>Output options can be edited.</summary>
    Configuring,
    /// <summary>Documents are being generated or validated.</summary>
    Generating,
    /// <summary>A generation cancellation is awaiting cleanup.</summary>
    CancellingGeneration,
    /// <summary>The validated batch is crossing the publication boundary.</summary>
    Publishing,
    /// <summary>The confirmed generation result is being displayed.</summary>
    Completed,
    /// <summary>Analysis was cancelled without adopting a plan.</summary>
    AnalysisCancelled,
    /// <summary>Analysis failed and the inputs can be corrected.</summary>
    AnalysisFailed,
    /// <summary>Generation was cancelled and the draft remains recoverable.</summary>
    GenerationCancelled,
    /// <summary>Generation failed and safe draft recovery remains available.</summary>
    GenerationFailed,
}

/// <summary>Contains the immutable presentation snapshot of one confirmed execution.</summary>
public sealed class GenerationResultViewModel
{
    /// <summary>Initializes a snapshot from the result and options actually executed.</summary>
    public GenerationResultViewModel(
        ClassGenerationResult result,
        IReadOnlyDictionary<ClassIdentifier, ClassHeaderValues> headers,
        string templateName)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        Headers = headers.ToDictionary(item => item.Key, item => item.Value);
        TemplateName = templateName;
    }

    /// <summary>Gets the confirmed application result.</summary>
    public ClassGenerationResult Result { get; }

    /// <summary>Gets the exact header values sent to generation.</summary>
    public IReadOnlyDictionary<ClassIdentifier, ClassHeaderValues> Headers { get; }

    /// <summary>Gets the non-sensitive template identity shown to the user.</summary>
    public string TemplateName { get; }

    /// <summary>Gets the final directory confirmed by publication.</summary>
    public string OutputDirectory => Result.OutputDirectory;

    /// <summary>Gets the number of generated documents.</summary>
    public int DocumentCount => Result.Documents.Count;

    /// <summary>Gets the number of students included in the confirmed plan.</summary>
    public int StudentCount => Result.Plan.Classes.Sum(item => item.Rows.Count);

    /// <summary>Gets the number of structured issues in the confirmed plan.</summary>
    public int IssueCount => Result.Plan.Issues.Count;

    /// <summary>Gets whether the confirmed plan contains issues.</summary>
    public bool HasIssues => IssueCount > 0;

    /// <summary>Gets a readable success summary.</summary>
    public string Summary => $"Se han creado {CountText(DocumentCount, "documento", "documentos")} "
        + $"para {CountText(StudentCount, "alumno", "alumnos")}.";

    /// <summary>Gets a compact read-only summary of executed header values.</summary>
    public string HeaderSummary => string.Join(
        Environment.NewLine,
        Headers.OrderBy(item => item.Key.CanonicalCode, StringComparer.Ordinal)
            .Select(item => string.IsNullOrEmpty(item.Value.TutorName)
                ? $"{item.Value.DisplayTitle} · sin tutor/a"
                : $"{item.Value.DisplayTitle} · {item.Value.TutorName}"));

    private static string CountText(int count, string singular, string plural) =>
        $"{count} {(count == 1 ? singular : plural)}";
}
