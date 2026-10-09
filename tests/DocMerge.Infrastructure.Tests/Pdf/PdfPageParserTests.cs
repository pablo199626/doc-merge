using DocMerge.Application.Pdf;
using DocMerge.Infrastructure.Pdf;

namespace DocMerge.Infrastructure.Tests.Pdf;

public sealed class PdfPageParserTests
{
    private readonly PdfPageParser _parser = new();

    [Fact]
    public void Parse_NumberedList_PreservesTraceability()
    {
        const string text = """
            Lista de nombres
            Tutora
            Ejemplo Responsable MARTA
            Alumnado (3)
            Nº
            Nombre y apellidos
            1
            Prueba Norte ANA
            2
            Ensayo Sur LUIS
            3
            Ficción Peña MARÍA ELENA
            """;

        var result = _parser.Parse([text]);

        Assert.Equal(3, result.DeclaredTotal);
        Assert.Equal("Prueba Norte ANA", result.Entries[0].OriginalText);
        Assert.Equal("página 1, entrada 1", result.Entries[0].Location);
        Assert.DoesNotContain(result.Entries, entry => entry.OriginalText.Contains("Responsable", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MultiplePagesAndWrappedName_JoinsName()
    {
        var result = _parser.Parse([
            "Alumnado (3)\nNº\nNombre y apellidos\n1\nPrueba Norte ANA\n2\nEnsayo Sur LUIS",
            "Lista de nombres\nAlumnado (3)\nNº\nNombre y apellidos\n3\nFicción Peña\nMARÍA ELENA",
        ]);

        Assert.Equal("Ficción Peña MARÍA ELENA", result.Entries[2].OriginalText);
        Assert.Equal(2, result.Entries[2].PageNumber);
    }

    [Fact]
    public void Parse_NormalizedDuplicate_RetainsBothReferences()
    {
        var result = _parser.Parse([
            "Alumnado (2)\nNombre y apellidos\n1\nFicción Peña MARÍA\n2\nFiccio\u0301n Pen\u0303a Mari\u0301a",
        ]);

        Assert.Single(result.Duplicates);
        Assert.Equal([1, 2], result.Duplicates.Single().Value.Select(entry => entry.Ordinal));
    }

    [Theory]
    [InlineData("Tutora\nEjemplo Responsable MARTA")]
    [InlineData("Alumnado (2)\nNombre y apellidos\n1\nPrueba Norte ANA")]
    [InlineData("Alumnado (2)\nNombre y apellidos\n1\nPrueba Norte ANA\n3\nEnsayo Sur LUIS")]
    [InlineData("Alumnado (0)\nNombre y apellidos")]
    public void Parse_MissingOrPartialExtraction_RejectsInput(string text)
    {
        Assert.Throws<PdfStructureException>(() => _parser.Parse([text]));
    }

    [Fact]
    public void Parse_NoExtractableText_RejectsInput()
    {
        Assert.Throws<PdfTextMissingException>(() => _parser.Parse([string.Empty, "   "]));
    }
}
