using System.Text.RegularExpressions;

namespace OoxmlValidate.Tests;

/// <summary>Single-file validation and its text output and exit codes.</summary>
public sealed class ValidateTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void ValidDocumentExitsZeroWithNoErrors()
    {
        var file = _dir.File("valid.docx");
        Docx.Write(file, Docx.Plain("First"), Docx.Plain("Second"));

        var r = CliRunner.Run(file);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("[Microsoft365]  opened  errors: 0", r.Stdout);
        Assert.Equal("", r.Stderr);
    }

    [Fact]
    public void BrokenDocumentExitsOneAndNamesTheError()
    {
        var file = _dir.File("broken.docx");
        Docx.Write(file, Docx.Plain("First"), Docx.WithBogus("Second"));

        var r = CliRunner.Run(file);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("opened  errors: 1", r.Stdout);
        Assert.Contains("1. [Schema] Sch_InvalidElementContentExpectingComplex", r.Stdout);
        Assert.Contains("bogus", r.Stdout);
        Assert.Matches(new Regex(@"xpath: /w:document\[1\]/w:body\[1\]/w:p\[2\]"), r.Stdout);
    }

    [Fact]
    public void MissingFileIsReportedAsAPackageError()
    {
        var file = _dir.File("absent.docx");

        var r = CliRunner.Run(file);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("NOT OPENABLE", r.Stdout);
        Assert.Contains("[Package] FileNotFound", r.Stdout);
    }

    [Fact]
    public void FileThatIsNotAZipIsReportedAsUnopenable()
    {
        var file = _dir.File("not-a-zip.docx");
        File.WriteAllText(file, "this is not a package");

        var r = CliRunner.Run(file);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("NOT OPENABLE", r.Stdout);
        Assert.Contains("[Package] PackageOpenFailed", r.Stdout);
    }

    [Fact]
    public void SeveralFilesPrintASummaryAndExitOneIfAnyIsBad()
    {
        var good = _dir.File("good.docx");
        var bad = _dir.File("bad.docx");
        Docx.Write(good, Docx.Plain("ok"));
        Docx.Write(bad, Docx.WithBogus("bad"));

        var r = CliRunner.Run(good, bad);

        Assert.Equal(1, r.ExitCode);
        Assert.Contains("SUMMARY", r.Stdout);
        Assert.Matches(new Regex(@"(?m)^\s+0\s+" + Regex.Escape(good) + "$"), r.Stdout);
        Assert.Matches(new Regex(@"(?m)^\s+1\s+" + Regex.Escape(bad) + "$"), r.Stdout);
    }

    [Fact]
    public void Office2019FormatIsAcceptedAndNamedInTheOutput()
    {
        var file = _dir.File("valid.docx");
        Docx.Write(file, Docx.Plain("First"));

        var r = CliRunner.Run("--format", "Office2019", file);

        Assert.Equal(0, r.ExitCode);
        Assert.Contains("[Office2019]", r.Stdout);
    }

    [Theory]
    [InlineData("--format", "Office2099")]
    [InlineData("--format=nope")]
    public void BadFormatIsAUsageError(params string[] args)
    {
        var r = CliRunner.Run(args.Append("x.docx").ToArray());

        Assert.Equal(2, r.ExitCode);
        Assert.Contains("bad or missing --format value", r.Stderr);
        Assert.Equal("", r.Stdout);
    }

    [Fact]
    public void NoFilesIsAUsageError()
    {
        var r = CliRunner.Run("--json");

        Assert.Equal(2, r.ExitCode);
        Assert.Contains("no input files", r.Stderr);
    }

    [Fact]
    public void UnknownOptionIsAUsageError()
    {
        var r = CliRunner.Run("--nope", "x.docx");

        Assert.Equal(2, r.ExitCode);
        Assert.Contains("unknown option: --nope", r.Stderr);
    }

    [Fact]
    public void HelpAndVersionExitZero()
    {
        var help = CliRunner.Run("--help");
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("--compare BASE.docx OUT.docx", help.Stdout);

        var version = CliRunner.Run("--version");
        Assert.Equal(0, version.ExitCode);
        Assert.Matches(@"^ooxml-validate \d+\.\d+\.\d+\s*$", version.Stdout);
    }
}
