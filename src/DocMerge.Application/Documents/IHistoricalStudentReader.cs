namespace DocMerge.Application.Documents;

/// <summary>Reads and prepares one historical DOCX for global student matching.</summary>
public interface IHistoricalStudentReader
{
    /// <summary>Reads one historical document.</summary>
    /// <param name="path">The historical DOCX path.</param>
    /// <param name="header">The exact student-name header.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated historical document snapshot.</returns>
    Task<HistoricalDocumentSnapshot> ReadAsync(
        string path,
        string header,
        CancellationToken cancellationToken);
}
