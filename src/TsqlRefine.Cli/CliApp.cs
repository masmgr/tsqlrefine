using System.Text;
using TsqlRefine.Cli.Services;

namespace TsqlRefine.Cli;

/// <summary>
/// Main entry point for the TsqlRefine CLI application.
/// </summary>
public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        EncodingProviderRegistry.EnsureRegistered();

        // Let System.CommandLine handle --help and --version automatically
        if (CliParser.IsHelpOrVersionRequest(args))
        {
            return await CliParser.InvokeAsync(args, stdout);
        }

        var parsed = CliParser.Parse(args);
        return await RunParsedAsync(parsed, stdin, stdout, stderr);
    }

    public static async Task<int> RunAsync(string[] args, Stream stdin, TextWriter stdout, TextWriter stderr)
    {
        EncodingProviderRegistry.EnsureRegistered();

        // Let System.CommandLine handle --help and --version automatically
        if (CliParser.IsHelpOrVersionRequest(args))
        {
            return await CliParser.InvokeAsync(args, stdout);
        }

        var parsed = CliParser.Parse(args);

        if (parsed.Stdin || parsed.Paths.Any(p => p == "-"))
        {
            if (parsed.DetectEncoding)
            {
                var decoded = await CharsetDetection.ReadStreamAsync(stdin);
                using var decodedReader = new StringReader(decoded.Text);
                return await RunParsedAsync(parsed, decodedReader, stdout, stderr);
            }

            using var streamReader = new StreamReader(
                stdin,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

            return await RunParsedAsync(parsed, streamReader, stdout, stderr);
        }

        return await RunParsedAsync(parsed, TextReader.Null, stdout, stderr);
    }

    private static async Task<int> RunParsedAsync(CliArgs parsed, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        // Require explicit subcommand
        if (!parsed.IsExplicitCommand)
        {
            await stderr.WriteLineAsync("Error: No command specified. Run 'tsqlrefine --help' for usage.");
            return ExitCodes.Fatal;
        }

        try
        {
            // Initialize services
            var inputReader = new InputReader();
            var commandExecutor = new CommandExecutor(inputReader);

            return parsed.Command switch
            {
                "init" => await CommandExecutor.ExecuteInitAsync(parsed, stdout, stderr),
                "print-config" => await CommandExecutor.ExecutePrintConfigAsync(parsed, stdout, stderr),
                "print-format-config" => await CommandExecutor.ExecutePrintFormatConfigAsync(parsed, stdout, stderr),
                "list-rules" => await CommandExecutor.ExecuteListRulesAsync(parsed, stdout, stderr),
                "list-plugins" => await CommandExecutor.ExecuteListPluginsAsync(parsed, stdout, stderr),
                "format" => await commandExecutor.ExecuteFormatAsync(parsed, stdin, stdout, stderr),
                "fix" => await commandExecutor.ExecuteFixAsync(parsed, stdin, stdout, stderr),
                "lint" => await commandExecutor.ExecuteLintAsync("lint", parsed, stdin, stdout, stderr),
                _ => await UnknownCommandAsync(parsed.Command, stderr)
            };
        }
        catch (ConfigException ex)
        {
            await stderr.WriteLineAsync(ex.Message);
            return ExitCodes.ConfigError;
        }

#pragma warning disable CA1031 // Top-level CLI error boundary
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await stderr.WriteLineAsync(ex.ToString());
            return ExitCodes.Fatal;
        }
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter stderr)
    {
        await stderr.WriteLineAsync($"Unknown command: '{command}'. Run 'tsqlrefine --help' for available commands.");
        return ExitCodes.Fatal;
    }
}
