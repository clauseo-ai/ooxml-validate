using System.Text.Json;

namespace OoxmlValidate.Tests;

/// <summary>The --json output of validate mode: one object on stdout.</summary>
public sealed class JsonTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void ValidDocumentGivesPassTrueAndNoErrors()
    {
        var file = _dir.File("valid.docx");
        Docx.Write(file, Docx.Plain("First"));

        var r = CliRunner.Run("--json", file);

        Assert.Equal(0, r.ExitCode);
        Assert.Equal("", r.Stderr);
        var root = JsonDocument.Parse(r.Stdout).RootElement;
        Assert.Equal("Microsoft365", root.GetProperty("format").GetString());
        Assert.True(root.GetProperty("pass").GetBoolean());
        var f = root.GetProperty("files")[0];
        Assert.True(f.GetProperty("opened").GetBoolean());
        Assert.Equal(0, f.GetProperty("errorCount").GetInt32());
        Assert.Empty(f.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void BrokenDocumentGivesPassFalseAndTheFindingFields()
    {
        var file = _dir.File("broken.docx");
        Docx.Write(file, Docx.Plain("First"), Docx.WithBogus("Second"));

        var r = CliRunner.Run("--json", file);

        Assert.Equal(1, r.ExitCode);
        var root = JsonDocument.Parse(r.Stdout).RootElement;
        Assert.False(root.GetProperty("pass").GetBoolean());
        var error = root.GetProperty("files")[0].GetProperty("errors")[0];
        Assert.Equal("Schema", error.GetProperty("errorType").GetString());
        Assert.Equal("Sch_InvalidElementContentExpectingComplex", error.GetProperty("id").GetString());
        Assert.Contains("bogus", error.GetProperty("description").GetString());
        Assert.Equal("/w:document[1]/w:body[1]/w:p[2]", error.GetProperty("xPath").GetString());
        Assert.Contains("/word/document.xml", error.GetProperty("partUri").GetString());
    }

    [Fact]
    public void SeveralFilesGiveOneEntryEach()
    {
        var a = _dir.File("a.docx");
        var b = _dir.File("b.docx");
        Docx.Write(a, Docx.Plain("a"));
        Docx.Write(b, Docx.WithBogus("b"));

        var r = CliRunner.Run("--json", a, b);

        var root = JsonDocument.Parse(r.Stdout).RootElement;
        Assert.False(root.GetProperty("pass").GetBoolean());
        var files = root.GetProperty("files");
        Assert.Equal(2, files.GetArrayLength());
        Assert.Equal(0, files[0].GetProperty("errorCount").GetInt32());
        Assert.Equal(1, files[1].GetProperty("errorCount").GetInt32());
    }
}
