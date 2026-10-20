namespace DocMerge.Domain.Generation;

/// <summary>Describes the expected document for one class.</summary>
public sealed record ClassDocumentPlan
{
    /// <summary>Initializes a new instance of the <see cref="ClassDocumentPlan"/> class.</summary>
    /// <param name="roster">The authoritative class roster.</param>
    /// <param name="rows">The planned student rows.</param>
    public ClassDocumentPlan(ClassRoster roster, IReadOnlyList<StudentRowPlan> rows)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(rows);
        Roster = roster;
        Rows = Array.AsReadOnly(rows.ToArray());
        IncludedRows = Array.AsReadOnly(Rows
            .Where(row => row.IsIncluded)
            .ToArray());
    }

    /// <summary>Gets the authoritative class roster.</summary>
    public ClassRoster Roster { get; }

    /// <summary>Gets the planned student rows.</summary>
    public IReadOnlyList<StudentRowPlan> Rows { get; }

    /// <summary>Gets rows that will be written to the generated document.</summary>
    public IReadOnlyList<StudentRowPlan> IncludedRows { get; }

    /// <summary>Gets the number of students omitted because their identity is conflicted.</summary>
    public int OmittedStudentCount => Rows.Count - IncludedRows.Count;

    /// <summary>Gets whether this class will produce a document.</summary>
    public bool ProducesDocument => IncludedRows.Count > 0;
}
