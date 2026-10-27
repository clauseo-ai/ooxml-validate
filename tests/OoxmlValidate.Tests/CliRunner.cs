namespace OoxmlValidate.Tests;

internal sealed record CliResult(int ExitCode, string Stdout, string Stderr);

internal static class CliRunner
{
    /// <summary>Runs the command line in-process, with its output captured.</summary>
    public static CliResult Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = OoxmlValidate.Cli.Run(args, stdout, stderr);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }
}
