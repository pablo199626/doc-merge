namespace DocMerge.Domain.Documents;

/// <summary>Represents a non-person row that must remain in the document.</summary>
/// <param name="RowIndex">The zero-based row index in the table.</param>
/// <param name="RowXml">The serialized WordprocessingML row.</param>
public sealed record AuxiliaryRow(int RowIndex, string RowXml);
