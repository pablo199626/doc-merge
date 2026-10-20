using DocMerge.Application.Pdf;
using DocMerge.Infrastructure.Pdf;

namespace DocMerge.Infrastructure.Tests.Pdf;

public sealed class GroupRosterPageParserTests
{
    [Fact]
    public void Parse_ValidGroupRoster_ReturnsClassAndTraceableEntries()
    {
        var pages = new[]
        {
            """
            CENTRO
            IES SOL
            GRUPO: 2ESOA - SEGUNDO ESO A
            Orden
            NIA
            Alumno
            Edad
            Curso
            1
            11353012
            ÁLVAREZ MOLINA, ENZO
            13
            2ESO
            2
            11660179
            PEÑA GÜEMES, MARÍA
            14
            2ESO
            """,
        };
        var parser = new GroupRosterPageParser();

        var result = parser.Parse(pages, "listado_grupo_2ESOA.pdf");

        Assert.Equal("2ESOA", result.ClassIdentifier.CanonicalCode);
        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("11353012", entry.StudentNumber);
                Assert.Equal("alvarez molina enzo", entry.MatchKey);
                Assert.Equal("página 1, entrada 1", entry.Location);
            },
            entry => Assert.Equal("peña guemes maria", entry.MatchKey));
    }

    [Fact]
    public void Parse_FileNameAndHeaderDisagree_ThrowsStructureException()
    {
        var pages = new[]
        {
            "GRUPO: 2ESOA\n1\n11353012\nALUMNO, UNO\n13\n2ESO",
        };
        var parser = new GroupRosterPageParser();

        var error = Assert.Throws<PdfStructureException>(
            () => parser.Parse(pages, "listado_grupo_2ESOB.pdf"));

        Assert.Contains("no coincide", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NonConsecutiveOrdinals_ThrowsStructureException()
    {
        var pages = new[]
        {
            "GRUPO: 2ESOA\n1\n11353012\nALUMNO, UNO\n13\n2ESO\n3\n11660179\nALUMNO, TRES\n13\n2ESO",
        };
        var parser = new GroupRosterPageParser();

        var error = Assert.Throws<PdfStructureException>(
            () => parser.Parse(pages, "listado_grupo_2ESOA.pdf"));

        Assert.Contains("no es consecutiva", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EntryCourseDisagreesWithClass_ThrowsStructureException()
    {
        var pages = new[]
        {
            "GRUPO: 2ESOA\n1\n11353012\nALUMNO, UNO\n13\n1ESO",
        };
        var parser = new GroupRosterPageParser();

        var error = Assert.Throws<PdfStructureException>(
            () => parser.Parse(pages, "listado_grupo_2ESOA.pdf"));

        Assert.Contains("pertenece al curso", error.Message, StringComparison.Ordinal);
    }
}
