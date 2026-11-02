# Changelog

## Unreleased

## 0.1.0 - 2026-11-02

First release.

- A command-line wrapper around the Open XML SDK validator (`OpenXmlValidator`,
  DocumentFormat.OpenXml 3.5.1).
- Validate one or more `.docx` files; `--json`; `--format NAME` (default `Microsoft365`).
- `--compare BASE OUT`: count-based verdict. A group of errors (Id, part and description with
  digits removed) is NEW only where OUT has more errors than BASE.
- `--version`, `--help`. Exit codes 0, 1 and 2 as in the README.
- Self-contained single-file builds for osx-arm64, osx-x64, linux-x64, linux-arm64 and win-x64,
  each with a `.sha256` file, and a Homebrew formula for the clauseo-ai tap.
