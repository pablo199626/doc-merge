using System.Collections;
using System.ComponentModel;
using DocMerge.Application.Generation;
using DocMerge.Domain.Generation;

namespace DocMerge.Wpf.ViewModels;

/// <summary>Represents editable output-header values for one stable class.</summary>
public sealed class ClassHeaderViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private string _displayTitle;
    private string _tutorName = string.Empty;

    /// <summary>Initializes one class header row.</summary>
    public ClassHeaderViewModel(ClassIdentifier classIdentifier, bool isEditable)
    {
        ClassIdentifier = classIdentifier ?? throw new ArgumentNullException(nameof(classIdentifier));
        IsEditable = isEditable;
        _displayTitle = ClassHeaderValidation.CreateDefaultTitle(classIdentifier);
    }

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>Gets the stable detected class.</summary>
    public ClassIdentifier ClassIdentifier { get; }

    /// <summary>Gets the detected canonical class code.</summary>
    public string DetectedClass => ClassIdentifier.CanonicalCode;

    /// <summary>Gets whether this template supports editing the fields.</summary>
    public bool IsEditable { get; }

    /// <summary>Gets or sets the visible class title.</summary>
    public string DisplayTitle
    {
        get => _displayTitle;
        set
        {
            if (SetProperty(ref _displayTitle, value))
            {
                NotifyValidationChanged(nameof(DisplayTitle));
            }
        }
    }

    /// <summary>Gets or sets the optional tutor name.</summary>
    public string TutorName
    {
        get => _tutorName;
        set
        {
            if (SetProperty(ref _tutorName, value))
            {
                NotifyValidationChanged(nameof(TutorName));
            }
        }
    }

    /// <inheritdoc />
    public bool HasErrors => IsEditable
        && (ClassHeaderValidation.GetTitleError(DisplayTitle) is not null
            || ClassHeaderValidation.GetTutorError(TutorName) is not null);

    /// <summary>Gets the current title validation message.</summary>
    public string? TitleError => IsEditable
        ? ClassHeaderValidation.GetTitleError(DisplayTitle)
        : null;

    /// <summary>Gets the current tutor validation message.</summary>
    public string? TutorError => IsEditable
        ? ClassHeaderValidation.GetTutorError(TutorName)
        : null;

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        if (!IsEditable)
        {
            return Array.Empty<string>();
        }

        var error = propertyName switch
        {
            nameof(DisplayTitle) => ClassHeaderValidation.GetTitleError(DisplayTitle),
            nameof(TutorName) => ClassHeaderValidation.GetTutorError(TutorName),
            _ => null,
        };
        return error is null ? Array.Empty<string>() : new[] { error };
    }

    /// <summary>Creates validated immutable application values.</summary>
    public ClassHeaderValues CreateValues() =>
        ClassHeaderValidation.Create(DisplayTitle, TutorName);

    private void NotifyValidationChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(propertyName == nameof(DisplayTitle)
            ? nameof(TitleError)
            : nameof(TutorError));
    }
}
