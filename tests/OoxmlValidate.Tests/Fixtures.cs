// Fixtures for the tests. Every .docx is made from scratch with the Open XML SDK, in a temporary
// directory that the test removes afterwards. Nothing is copied from the benchmark tree.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OoxmlValidate.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "ooxml-validate-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* a file still open on Windows; the runner's temp cleanup gets it */ }
    }
}

internal static class Docx
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>A plain paragraph. A body of these is a valid document.</summary>
    public static OpenXmlElement Plain(string text) => new Paragraph(new Run(new Text(text)));

    /// <summary>A paragraph with an element the schema does not allow in a paragraph: one error.</summary>
    public static OpenXmlElement WithBogus(string text) =>
        new Paragraph(new Run(new Text(text)), new OpenXmlUnknownElement("w", "bogus", W));

    /// <summary>Writes a .docx whose body holds the given block elements.</summary>
    public static void Write(string path, params OpenXmlElement[] body)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body(body));
        main.Document.Save();
    }
}

