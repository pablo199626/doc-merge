using DocMerge.Application.Errors;

namespace DocMerge.Application.Generation;

/// <summary>Identifies the generation input responsible for a preparation failure.</summary>
public enum GenerationInputType
{
    /// <summary>The historical document collection.</summary>
    HistoricalDocuments,
    /// <summary>The PDF roster collection.</summary>
    Rosters,
    /// <summary>The DOCX template.</summary>
    Template,
    /// <summary>The relationship between multiple inputs.</summary>
    InputSet,
}

/// <summary>Provides typed context for an input preparation failure.</summary>
public sealed class GenerationInputException : DocMergeException
{
    /// <summary>Initializes a contextual input failure while preserving its original message.</summary>
    public GenerationInputException(
        GenerationInputType inputType,
        string? path,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        InputType = inputType;
        Path = path;
    }

    /// <summary>Gets the affected input.</summary>
    public GenerationInputType InputType { get; }

    /// <summary>Gets the affected path, when one source is known.</summary>
    public string? Path { get; }
}
