using System.Collections.ObjectModel;

namespace DocMerge.Domain.Generation;

/// <summary>Represents a validated class assignment list extracted from one PDF.</summary>
public sealed record ClassRoster
{
    /// <summary>Initializes a new instance of the <see cref="ClassRoster"/> class.</summary>
    /// <param name="sourcePath">The source PDF path.</param>
    /// <param name="sha256">The lowercase SHA-256 hash of the PDF.</param>
    /// <param name="classIdentifier">The class identified by the PDF.</param>
    /// <param name="entries">The validated student assignments.</param>
    /// <param name="pageCount">The number of pages in the PDF.</param>
    /// <param name="hasDeclaredTotal"><see langword="true"/> when the PDF declares an independent total; otherwise, <see langword="false"/>.</param>
    public ClassRoster(
        string sourcePath,
        string sha256,
        ClassIdentifier classIdentifier,
        IReadOnlyList<ClassRosterEntry> entries,
        int pageCount,
        bool hasDeclaredTotal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(sha256);
        ArgumentNullException.ThrowIfNull(classIdentifier);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        SourcePath = sourcePath;
        Sha256 = sha256;
        ClassIdentifier = classIdentifier;
        Entries = Array.AsReadOnly(entries.ToArray());
        PageCount = pageCount;
        HasDeclaredTotal = hasDeclaredTotal;
        DuplicateStudentNumbers = CreateDuplicateStudentNumbers(Entries);
    }

    /// <summary>Gets the source PDF path.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the lowercase SHA-256 hash of the PDF.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the class identified by the PDF.</summary>
    public ClassIdentifier ClassIdentifier { get; }

    /// <summary>Gets the assignments in PDF order.</summary>
    public IReadOnlyList<ClassRosterEntry> Entries { get; }

    /// <summary>Gets the number of PDF pages.</summary>
    public int PageCount { get; }

    /// <summary>Gets a value that indicates whether the PDF declares an independent total.</summary>
    public bool HasDeclaredTotal { get; }

    /// <summary>Gets entries grouped by duplicated student number.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ClassRosterEntry>> DuplicateStudentNumbers { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<ClassRosterEntry>>
        CreateDuplicateStudentNumbers(IReadOnlyList<ClassRosterEntry> entries)
    {
        var duplicates = entries.GroupBy(entry => entry.StudentNumber, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ClassRosterEntry>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, IReadOnlyList<ClassRosterEntry>>(duplicates);
    }
}
