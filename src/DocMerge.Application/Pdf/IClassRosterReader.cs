using DocMerge.Domain.Generation;

namespace DocMerge.Application.Pdf;

/// <summary>Reads and validates the student assignments represented by a class PDF.</summary>
public interface IClassRosterReader
{
    /// <summary>Reads one class roster.</summary>
    /// <param name="path">The PDF path.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The validated class roster.</returns>
    Task<ClassRoster> ReadAsync(string path, CancellationToken cancellationToken);
}
