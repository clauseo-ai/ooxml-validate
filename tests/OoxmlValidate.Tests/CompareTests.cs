using System.Text.Json;
using DocumentFormat.OpenXml;

namespace OoxmlValidate.Tests;

/// <summary>
/// --compare BASE OUT: the count-based verdict. A group (Id + part + description with digits
/// removed) is NEW only where OUT has more errors than BASE, so an error that moves to another
/// XPath is not new, and an error that is fixed is only RESOLVED.
/// </summary>
public sealed class CompareTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Make(string name, params OpenXmlElement[] body)
    {
        var file = _dir.File(name);
        Docx.Write(file, body);
        return file;
    }

    [Fact]
    public void IdenticalFilesPass()
    {
        var file = Make("broken.docx", Docx.Plain("a"), Docx.WithBogus("b"));

        var r = CliRunner.Run("--compare", file, file);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("NEW in OUT: 0 error(s) in 0 group(s)", r.Stdout);
        Assert.Contains("RESULT: PASS (no new errors)", r.Stdout);
    }

    [Fact]
    public void ErrorThatMovesToAnotherXPathIsNotNew()
    {
        // The same bogus element, one paragraph further down: its XPath changes, its group does not.
        var baseFile = Make("base.docx", Docx.Plain("a"), Docx.WithBogus("b"));
        var outFile = Make("out.docx", Docx.Plain("inserted"), Docx.Plain("a"), Docx.WithBogus("b"));

        var r = CliRunner.Run("--compare", baseFile, outFile);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("RESULT: PASS (no new errors)", r.Stdout);

        var root = JsonDocument.Parse(CliRunner.Run("--json", "--compare", baseFile, outFile).Stdout).RootElement;
        Assert.True(root.GetProperty("pass").GetBoolean());
        Assert.Equal(0, root.GetProperty("newCount").GetInt32());
        Assert.Equal(0, root.GetProperty("resolvedCount").GetInt32());
        var baseX = root.GetProperty("base").GetProperty("errors")[0].GetProperty("xPath").GetString();
        var outX = root.GetProperty("out").GetProperty("errors")[0].GetProperty("xPath").GetString();
        Assert.Equal("/w:document[1]/w:body[1]/w:p[2]", baseX);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[3]", outX);
    }

    [Fact]
    public void AddedErrorIsNewAndFailsWithExitOne()
    {
        var baseFile = Make("base.docx", Docx.Plain("a"));
        var outFile = Make("out.docx", Docx.Plain("a"), Docx.WithBogus("b"));

        var r = CliRunner.Run("--compare", baseFile, outFile);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("NEW in OUT: 1 error(s) in 1 group(s)", r.Stdout);
        Assert.Contains("base 0 -> out 1", r.Stdout);
        Assert.Contains("xpath: /w:document[1]/w:body[1]/w:p[2]", r.Stdout);
        Assert.Contains("RESULT: FAIL (1 new error(s) in OUT)", r.Stdout);
    }

    [Fact]
    public void SecondErrorOfTheSameShapeIsNew()
    {
        // Count-based: one more error in an existing group is new even though its XPath is new too.
        var baseFile = Make("base.docx", Docx.WithBogus("a"));
        var outFile = Make("out.docx", Docx.WithBogus("a"), Docx.WithBogus("b"));

        var root = JsonDocument.Parse(CliRunner.Run("--json", "--compare", baseFile, outFile).Stdout).RootElement;

        Assert.False(root.GetProperty("pass").GetBoolean());
        Assert.Equal(1, root.GetProperty("newCount").GetInt32());
        var group = root.GetProperty("new")[0];
        Assert.Equal(1, group.GetProperty("base").GetInt32());
        Assert.Equal(2, group.GetProperty("out").GetInt32());
        Assert.Equal(1, group.GetProperty("delta").GetInt32());
    }

    [Fact]
    public void TwoErrorsReplacedByTwoOfTheSameShapePass()
    {
        // The count per group is the same (2 and 2), so nothing is new, however the errors moved.
        var baseFile = Make("base.docx", Docx.WithBogus("a"), Docx.WithBogus("b"));
        var outFile = Make("out.docx", Docx.WithBogus("c"), Docx.Plain("d"), Docx.WithBogus("e"));

        var r = CliRunner.Run("--compare", baseFile, outFile);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("RESULT: PASS", r.Stdout);
    }

    [Fact]
    public void FixedErrorIsResolvedAndPasses()
    {
        var baseFile = Make("base.docx", Docx.Plain("a"), Docx.WithBogus("b"));
        var outFile = Make("out.docx", Docx.Plain("a"), Docx.Plain("b"));

        var r = CliRunner.Run("--compare", baseFile, outFile);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("RESOLVED (fewer in OUT): 1 error(s) in 1 group(s)", r.Stdout);
        Assert.Contains("RESULT: PASS", r.Stdout);

        var root = JsonDocument.Parse(CliRunner.Run("--json", "--compare", baseFile, outFile).Stdout).RootElement;
        Assert.True(root.GetProperty("pass").GetBoolean());
        Assert.Equal(1, root.GetProperty("resolvedCount").GetInt32());
        Assert.Equal(-1, root.GetProperty("resolved")[0].GetProperty("delta").GetInt32());
    }

    [Fact]
    public void OutThatStopsOpeningIsNewAndFails()
    {
        var baseFile = Make("base.docx", Docx.Plain("a"));
        var outFile = _dir.File("out.docx");
        File.WriteAllText(outFile, "truncated");

        var r = CliRunner.Run("--compare", baseFile, outFile);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("OUT   ", r.Stdout);
        Assert.Contains("NOT OPENABLE", r.Stdout);
        Assert.Contains("[Package] PackageOpenFailed", r.Stdout);
    }

    [Theory]
    [InlineData("one.docx")]
    [InlineData("one.docx", "two.docx", "three.docx")]
    public void CompareNeedsExactlyTwoFiles(params string[] files)
    {
        var r = CliRunner.Run(new[] { "--compare" }.Concat(files).ToArray());

        Assert.Equal(2, r.ExitCode);
        Assert.Contains("--compare needs exactly two files", r.Stderr);
    }
}
