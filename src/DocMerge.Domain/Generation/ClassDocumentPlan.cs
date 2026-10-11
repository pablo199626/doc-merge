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
    }

    /// <summary>Gets the authoritative class roster.</summary>
    public ClassRoster Roster { get; }

    /// <summary>Gets the planned student rows.</summary>
    public IReadOnlyList<StudentRowPlan> Rows { get; }
}
