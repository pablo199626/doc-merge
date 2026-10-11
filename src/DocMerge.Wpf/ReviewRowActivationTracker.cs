using System.Windows;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf;

/// <summary>Tracks whether one pointer press and release represents a deliberate row activation.</summary>
internal sealed class ReviewRowActivationTracker
{
    private StudentReviewViewModel? _pressedStudent;
    private Point _pressedPosition;
    private bool _hasExceededDragThreshold;

    /// <summary>Starts tracking one non-interactive row press.</summary>
    public void Begin(StudentReviewViewModel student, Point position)
    {
        _pressedStudent = student ?? throw new ArgumentNullException(nameof(student));
        _pressedPosition = position;
        _hasExceededDragThreshold = false;
    }

    /// <summary>Records movement while the pointer press remains active.</summary>
    public void Update(Point position, Size dragThreshold)
    {
        if (_pressedStudent is null || _hasExceededDragThreshold)
        {
            return;
        }

        _hasExceededDragThreshold = HasExceededThreshold(position, dragThreshold);
    }

    /// <summary>Completes and clears the current gesture.</summary>
    public StudentReviewViewModel? Complete(
        StudentReviewViewModel releasedStudent,
        Point releasedPosition,
        Size dragThreshold)
    {
        ArgumentNullException.ThrowIfNull(releasedStudent);
        Update(releasedPosition, dragThreshold);
        var pressedStudent = _pressedStudent;
        var hasExceededDragThreshold = _hasExceededDragThreshold;
        Reset();
        if (!ReferenceEquals(pressedStudent, releasedStudent) || hasExceededDragThreshold)
        {
            return null;
        }

        return releasedStudent;
    }

    /// <summary>Clears a pending press that cannot activate a row.</summary>
    public void Reset()
    {
        _pressedStudent = null;
        _hasExceededDragThreshold = false;
    }

    private bool HasExceededThreshold(Point position, Size dragThreshold)
    {
        var movement = position - _pressedPosition;
        return Math.Abs(movement.X) > dragThreshold.Width
            || Math.Abs(movement.Y) > dragThreshold.Height;
    }
}
