// ooxml-validate: command-line wrapper around the Open XML SDK validator (OpenXmlValidator).
// The validation itself is Microsoft's; this file only reads arguments, runs it and prints the result.
//
// See Cli.cs for the modes and exit codes.

namespace OoxmlValidate;

internal static class Program
{
    private static int Main(string[] args) => Cli.Run(args, Console.Out, Console.Error);
}
