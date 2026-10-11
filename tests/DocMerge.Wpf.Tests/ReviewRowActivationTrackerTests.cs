using System.Windows;
using DocMerge.Domain.Generation;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests;

public sealed class ReviewRowActivationTrackerTests
{
    [Fact]
    public void Complete_SameStudentWithinThreshold_ReturnsStudent()
    {
        var student = CreateStudent("12345678");
        var tracker = new ReviewRowActivationTracker();
        tracker.Begin(student, new Point(10, 10));

        var result = tracker.Complete(student, new Point(13, 14), new Size(4, 4));

        Assert.Same(student, result);
    }

    [Fact]
    public void Complete_MovementExceedsThreshold_DoesNotActivate()
    {
        var student = CreateStudent("12345678");
        var tracker = new ReviewRowActivationTracker();
        tracker.Begin(student, new Point(10, 10));

        var result = tracker.Complete(student, new Point(20, 10), new Size(4, 4));

        Assert.Null(result);
    }

    [Fact]
    public void Complete_PointerReturnsAfterExceedingThreshold_DoesNotActivate()
    {
        var student = CreateStudent("12345678");
        var tracker = new ReviewRowActivationTracker();
        tracker.Begin(student, new Point(10, 10));
        tracker.Update(new Point(20, 10), new Size(4, 4));

        var result = tracker.Complete(student, new Point(10, 10), new Size(4, 4));

        Assert.Null(result);
    }

    [Fact]
    public void Complete_DifferentReleasedStudent_DoesNotActivate()
    {
        var pressedStudent = CreateStudent("12345678");
        var releasedStudent = CreateStudent("87654321");
        var tracker = new ReviewRowActivationTracker();
        tracker.Begin(pressedStudent, new Point(10, 10));

        var result = tracker.Complete(releasedStudent, new Point(10, 10), new Size(4, 4));

        Assert.Null(result);
    }

    private static StudentReviewViewModel CreateStudent(string studentNumber)
    {
        var classIdentifier = new ClassIdentifier("ESO", 2, "A");
        var entry = new ClassRosterEntry(
            "ALUMNO, FICTICIO",
            "alumno, ficticio",
            "alumno ficticio",
            studentNumber,
            1,
            1);
        var resolution = new HistoricalResolution(
            HistoricalResolutionType.MissingHistoricalStudent,
            [],
            selectedCandidate: null);
        return new StudentReviewViewModel(
            classIdentifier,
            new StudentRowPlan(entry, resolution),
            []);
    }
}
