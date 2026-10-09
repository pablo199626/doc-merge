using DocMerge.Console.Cli;

namespace DocMerge.Console.Tests.Cli;

public sealed class CliParserTests
{
    private readonly CliParser _parser = new();

    [Fact]
    public void Parse_DocumentedAliasesAndEqualsForm_ReturnsOptions()
    {
        var options = _parser.Parse([
            "source.docx",
            "--destino=destination.docx",
            "--lista", "list.pdf",
            "--output", "result.docx",
            "--simular",
            "--force",
        ]);

        Assert.Equal("source.docx", options.Source);
        Assert.Equal("destination.docx", options.Destination);
        Assert.Equal("list.pdf", options.PdfList);
        Assert.Equal("result.docx", options.Output);
        Assert.True(options.Simulate);
        Assert.True(options.Force);
    }

    [Fact]
    public void Parse_OptionRepeated_LastValueWinsLikeArgparse()
    {
        var options = _parser.Parse(["source.docx", "-o", "first.docx", "-o", "last.docx"]);

        Assert.Equal("last.docx", options.Output);
    }

    [Fact]
    public void Parse_DoubleDash_AllowsDashPrefixedSource()
    {
        var options = _parser.Parse(["--", "-source.docx"]);

        Assert.Equal("-source.docx", options.Source);
    }

    [Fact]
    public void Parse_ArgparseAbbreviation_IsNotSupportedByApprovedContract()
    {
        Assert.Throws<CliUsageException>(() => _parser.Parse(["--des", "destination.docx"]));
    }
}
