// ooxml-validate: command-line wrapper around the Open XML SDK validator (OpenXmlValidator).
//
//   ooxml-validate FILE.docx [FILE ...] [--format NAME] [--json]
//   ooxml-validate --compare BASE.docx OUT.docx [--format NAME] [--json]
//
// Exit codes: 0 = clean (or, with --compare, no new errors); 1 = errors found,
// new errors, or a file the SDK cannot open; 2 = usage error.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

internal sealed record Finding(string ErrorType, string Id, string Description, string? PartUri, string? XPath);

internal sealed class FileResult
{
    public required string File { get; init; }
    public required string Format { get; init; }
    public bool Opened { get; set; }
    public double Seconds { get; set; }
    public List<Finding> Errors { get; } = new();
    public int ErrorCount => Errors.Count;
}

internal sealed record GroupRow(string ErrorType, string Id, string? PartUri, string Description, int Base, int Out, int Delta, List<string?> XPaths);

internal static class Program
{
    private const int ExitOk = 0, ExitFail = 1, ExitUsage = 2;
    // Validator cap; large enough that real benchmark outputs are never truncated.
    private const int MaxErrors = 100000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string Usage = """
        ooxml-validate - validate .docx files with the Open XML SDK (OpenXmlValidator)

        Usage:
          ooxml-validate FILE.docx [FILE ...] [--format NAME] [--json]
          ooxml-validate --compare BASE.docx OUT.docx [--format NAME] [--json]

        Options:
          --format NAME  FileFormatVersions to validate against (default Microsoft365).
                         One of: Office2007 Office2010 Office2013 Office2016 Office2019 Office2021 Microsoft365
          --json         machine-readable output on stdout
          --compare      report errors present in OUT but not in BASE (give exactly two files)
          -h, --help     this text

        Compare matching: errors are grouped by Id + PartUri + description with every digit
        run replaced by '#'. A group is NEW only where OUT has more errors than BASE (count
        delta > 0); XPaths are printed as detail. Errors that moved to another XPath therefore
        do not count as new. Exit 1 if any group is NEW.

        Exit codes: 0 = clean / no new errors; 1 = errors / new errors / unopenable file; 2 = usage.

        """;

    private static int Main(string[] args)
    {
        var files = new List<string>();
        var json = false;
        var compare = false;
        var format = FileFormatVersions.Microsoft365;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help") { Console.Write(Usage); return ExitOk; }
            if (a == "--json") { json = true; continue; }
            if (a == "--compare") { compare = true; continue; }
            if (a == "--format" || a.StartsWith("--format=", StringComparison.Ordinal))
            {
                string? value = a == "--format" ? (++i < args.Length ? args[i] : null) : a["--format=".Length..];
                if (value is null || !Enum.TryParse(value, ignoreCase: true, out FileFormatVersions parsed)
                    || parsed == FileFormatVersions.None)
                    return UsageError($"bad or missing --format value: {value ?? "(none)"}");
                format = parsed;
                continue;
            }
            if (a.StartsWith('-') && a != "-") return UsageError($"unknown option: {a}");
            files.Add(a);
        }

        if (files.Count == 0) return UsageError("no input files");
        if (compare && files.Count != 2) return UsageError("--compare needs exactly two files: BASE OUT");

        var validator = new OpenXmlValidator(format) { MaxNumberOfErrors = MaxErrors };

        return compare
            ? RunCompare(files[0], files[1], format, validator, json)
            : RunValidate(files, format, validator, json);
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"ooxml-validate: {message}");
        Console.Error.WriteLine("Try 'ooxml-validate --help'.");
        return ExitUsage;
    }

    // ---- validate mode -------------------------------------------------------

    private static int RunValidate(List<string> files, FileFormatVersions format, OpenXmlValidator validator, bool json)
    {
        var results = files.Select(f => ValidateFile(f, format, validator)).ToList();
        var anyBad = results.Any(r => r.ErrorCount > 0);

        if (json)
        {
            var doc = new { format = format.ToString(), pass = !anyBad, files = results };
            Console.WriteLine(JsonSerializer.Serialize(doc, JsonOpts));
        }
        else
        {
            var sb = new StringBuilder();
            foreach (var r in results)
            {
                sb.AppendLine($"== {r.File}  [{r.Format}]  {(r.Opened ? "opened" : "NOT OPENABLE")}  errors: {r.ErrorCount}  ({r.Seconds:0.00}s)");
                for (int i = 0; i < r.Errors.Count; i++)
                {
                    sb.Append($"  {i + 1}. ");
                    AppendFinding(sb, r.Errors[i], "     ");
                }
            }
            if (results.Count > 1)
            {
                sb.AppendLine("SUMMARY");
                foreach (var r in results)
                    sb.AppendLine($"  {r.ErrorCount,6}  {(r.Opened ? "" : "[unopenable] ")}{r.File}");
            }
            Console.Write(sb);
        }
        return anyBad ? ExitFail : ExitOk;
    }

    // ---- compare mode --------------------------------------------------------

    // A finding's group: Id + PartUri + description with every digit run replaced by '#'.
    // Element names are kept; indices (XPath positions, values like '22') are not, so an edit
    // that moves an existing error to another XPath lands in the same group.
    private static string GroupKey(Finding f) =>
        $"{f.Id}\u001f{f.PartUri ?? ""}\u001f{Digits.Replace(f.Description, "#")}";

    private static int RunCompare(string basePath, string outPath, FileFormatVersions format, OpenXmlValidator validator, bool json)
    {
        var baseRes = ValidateFile(basePath, format, validator);
        var outRes = ValidateFile(outPath, format, validator);

        var baseG = baseRes.Errors.GroupBy(GroupKey).ToDictionary(g => g.Key, g => g.ToList());
        var outG = outRes.Errors.GroupBy(GroupKey).ToDictionary(g => g.Key, g => g.ToList());

        // Verdict is count-based: a group is NEW only where OUT has more errors than BASE.
        var groups = baseG.Keys.Union(outG.Keys, StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k =>
            {
                var b = baseG.GetValueOrDefault(k) ?? new List<Finding>();
                var o = outG.GetValueOrDefault(k) ?? new List<Finding>();
                var sample = o.FirstOrDefault() ?? b[0];
                return new GroupRow(sample.ErrorType, sample.Id, sample.PartUri, sample.Description,
                    b.Count, o.Count, o.Count - b.Count,
                    o.Select(f => f.XPath).ToList());
            })
            .Where(r => r.Delta != 0)
            .ToList();

        var added = groups.Where(r => r.Delta > 0).OrderByDescending(r => r.Delta).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var resolved = groups.Where(r => r.Delta < 0).OrderBy(r => r.Delta).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var newCount = added.Sum(r => r.Delta);
        var resolvedCount = -resolved.Sum(r => r.Delta);
        var pass = newCount == 0;

        if (json)
        {
            var doc = new
            {
                format = format.ToString(),
                pass,
                verdict = "count-based: new = positive delta per (Id, Part, normalised description)",
                @base = baseRes,
                @out = outRes,
                newCount,
                @new = added,
                resolvedCount,
                resolved,
            };
            Console.WriteLine(JsonSerializer.Serialize(doc, JsonOpts));
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine($"BASE  {baseRes.File}  [{baseRes.Format}]  {(baseRes.Opened ? "opened" : "NOT OPENABLE")}  errors: {baseRes.ErrorCount}  ({baseRes.Seconds:0.00}s)");
            sb.AppendLine($"OUT   {outRes.File}  [{outRes.Format}]  {(outRes.Opened ? "opened" : "NOT OPENABLE")}  errors: {outRes.ErrorCount}  ({outRes.Seconds:0.00}s)");
            sb.AppendLine("Group = Id + part + description with digits removed (count-based; XPath shown as detail only).");
            sb.AppendLine();
            sb.AppendLine($"NEW in OUT: {newCount} error(s) in {added.Count} group(s)");
            foreach (var r in added) AppendGroup(sb, r, withXPaths: true);
            sb.AppendLine($"RESOLVED (fewer in OUT): {resolvedCount} error(s) in {resolved.Count} group(s)");
            foreach (var r in resolved) AppendGroup(sb, r, withXPaths: false);
            sb.AppendLine(pass ? "RESULT: PASS (no new errors)" : $"RESULT: FAIL ({newCount} new error(s) in OUT)");
            Console.Write(sb);
        }
        return pass ? ExitOk : ExitFail;
    }

    private static void AppendGroup(StringBuilder sb, GroupRow r, bool withXPaths)
    {
        sb.AppendLine($"  {r.Delta:+0;-0}  base {r.Base} -> out {r.Out}  [{r.ErrorType}] {r.Id}  part: {r.PartUri ?? "-"}");
        sb.AppendLine($"        desc: {r.Description}");
        if (withXPaths)
            foreach (var x in r.XPaths.Where(x => x is not null).Distinct())
                sb.AppendLine($"        xpath: {x}");
    }

    private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);

    // ---- validation of one file ----------------------------------------------

    private static FileResult ValidateFile(string path, FileFormatVersions format, OpenXmlValidator validator)
    {
        var result = new FileResult { File = path, Format = format.ToString() };
        var sw = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(path))
            {
                result.Errors.Add(new Finding("Package", "FileNotFound", "File does not exist", null, null));
                return result;
            }

            using var doc = WordprocessingDocument.Open(path, false);
            result.Opened = true;
            try
            {
                foreach (var e in validator.Validate(doc))
                    result.Errors.Add(ToFinding(e));
            }
            catch (Exception ex)
            {
                // A part that does not parse (e.g. malformed XML) can throw mid-validation.
                result.Errors.Add(new Finding("Package", "ValidatorException", Flatten(ex), null, null));
            }
        }
        catch (Exception ex)
        {
            // Not a zip, missing main document part, wrong package type, truncated file, ...
            result.Errors.Add(new Finding("Package", "PackageOpenFailed", Flatten(ex), null, null));
        }
        finally
        {
            sw.Stop();
            result.Seconds = sw.Elapsed.TotalSeconds;
        }
        return result;
    }

    private static Finding ToFinding(ValidationErrorInfo e) => new(
        e.ErrorType.ToString(),
        e.Id ?? "",
        Oneline(e.Description),
        e.Path?.PartUri?.ToString() ?? e.Part?.Uri.ToString(),
        string.IsNullOrEmpty(e.Path?.XPath) ? null : e.Path!.XPath);

    private static string Flatten(Exception ex) => Oneline($"{ex.GetType().Name}: {ex.Message}");

    private static string Oneline(string? s) =>
        string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, @"\s+", " ").Trim();

    private static void AppendFinding(StringBuilder sb, Finding f, string indent)
    {
        sb.AppendLine($"[{f.ErrorType}] {f.Id}");
        sb.AppendLine($"{indent}part:  {f.PartUri ?? "-"}");
        sb.AppendLine($"{indent}xpath: {f.XPath ?? "-"}");
        sb.AppendLine($"{indent}desc:  {f.Description}");
    }
}
