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
