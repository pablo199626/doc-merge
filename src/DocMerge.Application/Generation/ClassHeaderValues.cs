using System.Collections.ObjectModel;
using System.Globalization;
using DocMerge.Application.Validation;
using DocMerge.Domain.Generation;

namespace DocMerge.Application.Generation;

/// <summary>Contains the visible document-header values for one class.</summary>
public sealed record ClassHeaderValues(string DisplayTitle, string TutorName);

/// <summary>Contains an immutable snapshot of document-header values for a prepared batch.</summary>
public sealed class ClassGenerationOutputOptions
{
    /// <summary>Initializes options bound to one prepared batch.</summary>
    public ClassGenerationOutputOptions(
        PreparedClassGeneration prepared,
        IReadOnlyDictionary<ClassIdentifier, ClassHeaderValues> headers)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(headers);
        Prepared = prepared;
        var expected = prepared.Plan.Classes.Select(item => item.Roster.ClassIdentifier).ToHashSet();
        if (headers.Count != expected.Count || headers.Keys.Any(identifier => !expected.Contains(identifier)))
        {
            throw new OperationValidationException(
                "Las cabeceras no corresponden exactamente con las clases del lote revisado.");
        }

        var copy = headers.ToDictionary(
            item => item.Key,
            item => ClassHeaderValidation.Create(item.Value.DisplayTitle, item.Value.TutorName));
        Headers = new ReadOnlyDictionary<ClassIdentifier, ClassHeaderValues>(copy);
    }

    /// <summary>Gets the exact prepared batch to which these options belong.</summary>
    public PreparedClassGeneration Prepared { get; }

    /// <summary>Gets values indexed by stable class identifier.</summary>
    public IReadOnlyDictionary<ClassIdentifier, ClassHeaderValues> Headers { get; }
}

/// <summary>Validates and formats visible class-document headers.</summary>
public static class ClassHeaderValidation
{
    /// <summary>The maximum supported visible class-title length.</summary>
    public const int MaximumTitleLength = 60;

    /// <summary>The maximum supported tutor-name length.</summary>
    public const int MaximumTutorLength = 120;

    /// <summary>Creates validated values without altering non-empty user text.</summary>
    public static ClassHeaderValues Create(string displayTitle, string? tutorName)
    {
        var titleError = GetTitleError(displayTitle);
        if (titleError is not null)
        {
            throw new OperationValidationException(titleError);
        }

        var tutorError = GetTutorError(tutorName);
        if (tutorError is not null)
        {
            throw new OperationValidationException(tutorError);
        }

        return new ClassHeaderValues(displayTitle, string.IsNullOrWhiteSpace(tutorName) ? string.Empty : tutorName);
    }

    /// <summary>Returns an actionable title error, if any.</summary>
    public static string? GetTitleError(string? value) => Validate(
        value,
        isRequired: true,
        MaximumTitleLength,
        "El nombre que aparecerá en el documento");

    /// <summary>Returns an actionable tutor error, if any.</summary>
    public static string? GetTutorError(string? value) => Validate(
        value,
        isRequired: false,
        MaximumTutorLength,
        "El nombre del tutor o tutora");

    /// <summary>Creates a readable default without changing class identity.</summary>
    public static string CreateDefaultTitle(ClassIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return string.Equals(identifier.Stage, "ESO", StringComparison.Ordinal)
            ? $"{identifier.Level}º ESO {identifier.Group}"
            : identifier.CanonicalCode;
    }

    private static string? Validate(string? value, bool isRequired, int maximumLength, string label)
    {
        if (isRequired && string.IsNullOrWhiteSpace(value))
        {
            return $"{label} no puede estar vacío.";
        }

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Any(character => character is '\r' or '\n' or '\t' || char.IsControl(character)))
        {
            return $"{label} debe escribirse en una sola línea y sin caracteres de control.";
        }

        return new StringInfo(value).LengthInTextElements > maximumLength
            ? $"{label} no puede superar {maximumLength} caracteres."
            : null;
    }
}
