# ooxml-validate

A command-line wrapper around Microsoft's Open XML SDK validator. It runs `OpenXmlValidator` from
DocumentFormat.OpenXml on `.docx` files, prints what it finds, and adds two things the SDK does not:
a JSON output and a `--compare` mode that says whether an edited file has new errors.

The validation itself is Microsoft's. This tool does not check the OOXML schema or the package
itself; it only reads arguments, calls the SDK, groups the results and sets the exit code.

## Install

### From source (now)

You need the .NET 10 SDK. From a clone of this repository:

```sh
dotnet test                       # runs the tests
dotnet run --project src/OoxmlValidate -- FILE.docx

# A single self-contained file, no .NET needed on the machine that runs it:
dotnet publish src/OoxmlValidate -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
./out/ooxml-validate --version
```

Replace `osx-arm64` with `osx-x64`, `linux-x64`, `linux-arm64` or `win-x64` for another platform.

### Homebrew and release binaries (with the first release)

These are not published yet. Once a release is out, they will be:

```sh
brew install clauseo-ai/tap/ooxml-validate
```

and a GitHub release will carry self-contained archives for macOS (arm64, x64), Linux (x64, arm64)
and Windows (x64), each with a `.sha256` file. The Homebrew formula template is in
`packaging/homebrew/`.

## Usage

```
ooxml-validate FILE.docx [FILE ...] [--format NAME] [--json]
ooxml-validate --compare BASE.docx OUT.docx [--format NAME] [--json]
ooxml-validate --version | --help
```

| Option | Meaning |
| --- | --- |
| `--format NAME` | The `FileFormatVersions` to validate against. One of `Office2007`, `Office2010`, `Office2013`, `Office2016`, `Office2019`, `Office2021`, `Microsoft365`. The default is `Microsoft365`. Names are case-insensitive. |
| `--json` | One JSON object on stdout instead of text. |
| `--compare` | Give exactly two files, BASE then OUT. Reports the errors in OUT that are new compared with BASE. |
| `--version` | Print `ooxml-validate <version>` and exit. |
| `-h`, `--help` | Print the usage text. |

### Validate

```sh
ooxml-validate contract.docx
ooxml-validate --format Office2019 contract.docx
ooxml-validate --json contract.docx other.docx
```

### Compare

```sh
ooxml-validate --compare original.docx edited.docx
```

The verdict is count-based. Each error is put in a group by its `Id`, its part and its description
with every run of digits replaced by `#`. A group is **NEW** only where OUT has more errors in it than
BASE does. A group that has fewer errors in OUT is **RESOLVED**. XPaths are printed under NEW groups
as detail, and they do not decide the verdict.

Why not compare XPaths? An edit moves errors that were already in the file. Inserting one paragraph
before a broken one changes its `w:p[12]` to `w:p[13]`, so an XPath comparison would report an error
that was already there as new. The count-based verdict does not. The cost: if one error is fixed and
a different one of the same shape (same `Id`, part and description) appears, the count stays the same
and the verdict is PASS. The RESOLVED and NEW lines show the group, so a reader can see it.

## Output

Validate, text (one block per file, then a SUMMARY when there are several):

```
== contract.docx  [Microsoft365]  opened  errors: 1  (0.28s)
  1. [Schema] Sch_UnexpectedElementContentExpectingComplex
     part:  /word/document.xml
     xpath: /w:document[1]/w:body[1]/w:tbl[1]/w:tblPr[1]
     desc:  The element has unexpected child element '...:tblBorders'.
SUMMARY
       1  contract.docx
```

A file the SDK cannot open is reported with `NOT OPENABLE` and an error with the `Package` type, for
example `Package/PackageOpenFailed` or `Package/FileNotFound`. If the SDK throws part-way through a
file, the exception is reported as `Package/ValidatorException`, and the errors it found before that
are kept.

Compare, text: `BASE` and `OUT` lines, then `NEW in OUT: N error(s) in M group(s)` with each group's
count (`base x -> out y`), part, description and XPaths; then `RESOLVED`; then `RESULT: PASS` or
`RESULT: FAIL (N new error(s) in OUT)`.

JSON, validate:

```json
{
  "format": "Microsoft365",
  "pass": false,
  "files": [
    {
      "file": "contract.docx", "format": "Microsoft365", "opened": true, "seconds": 0.28,
      "errors": [
        { "errorType": "Schema", "id": "Sch_...", "description": "...",
          "partUri": "/word/document.xml", "xPath": "/w:document[1]/..." }
      ],
      "errorCount": 1
    }
  ]
}
```

JSON, compare: `format`, `pass`, `verdict`, `base` and `out` (each a file object as above),
`newCount`, `new` (groups with `errorType`, `id`, `partUri`, `description`, `base`, `out`, `delta`,
`xPaths`), `resolvedCount` and `resolved` (the same fields, with negative `delta`).

## Exit codes

| Code | Validate | Compare |
| --- | --- | --- |
| 0 | every file has no errors | no group is NEW (RESOLVED errors are fine) |
| 1 | any error, or a file the SDK cannot open | any NEW group (an OUT that cannot be opened is one, unless BASE has the same error) |
| 2 | usage error (no file, bad option, bad `--format`, not exactly two files with `--compare`) | same |

## Limits

- **It is not Word.** The SDK checks the file against the OOXML schema for the chosen format. Word
  may open and repair a file this tool flags, and may reject a file it passes. Use the result as a
  schema check, not as a promise about how Word will show the document.
- **`.docx` only.** Other OOXML packages (`.xlsx`, `.pptx`) are reported as `PackageOpenFailed`.
- **The SDK can stop early.** When a part throws during validation, the errors after it are not
  listed. A low count can understate the problems in such a file; the abort is reported as an error.
- **Error cap.** The tool sets the validator's error limit to 100,000 per file.
- **The count-based verdict** can miss a swap of one error for another of the same shape, as above.
- **Timing.** About 0.3 s per file including .NET start-up on Apple silicon. Compare validates two
  files, one after the other.

## Used by

The stetdoc docx benchmark uses it to check the `.docx` files its agents produce, with `--compare`
against the input document. See [stetdoc](https://github.com/clauseo-ai/stetdoc).

## Development

```sh
dotnet test
```

The tests build every `.docx` they use from scratch with the Open XML SDK, so no third-party
documents are needed. They run the command in-process and cover single files, `--json`, `--compare`
verdicts, and the exit codes.

## Licence

MIT, see [LICENSE](LICENSE). The Open XML SDK is MIT-licensed too; see [NOTICE](NOTICE).
