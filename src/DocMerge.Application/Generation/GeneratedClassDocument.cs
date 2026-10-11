using DocMerge.Domain.Generation;

namespace DocMerge.Application.Generation;

/// <summary>Identifies one temporarily generated class document.</summary>
/// <param name="ClassIdentifier">The generated class.</param>
/// <param name="Path">The temporary DOCX path.</param>
public sealed record GeneratedClassDocument(ClassIdentifier ClassIdentifier, string Path);
