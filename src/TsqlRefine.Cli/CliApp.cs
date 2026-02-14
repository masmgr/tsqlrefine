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
        var (parsed, handledExitCode) = await ParseOrHandleBuiltInAsync(args, stdout);
        if (handledExitCode is not null)
        {
            return handledExitCode.Value;
        }

        return await RunParsedAsync(parsed ?? throw new InvalidOperationException("Parsed args were not available."), stdin, stdout, stderr);
    }

    public static async Task<int> RunAsync(string[] args, Stream stdin, TextWriter stdout, TextWriter stderr)
    {
        var (parsed, handledExitCode) = await ParseOrHandleBuiltInAsync(args, stdout);
        if (handledExitCode is not null)
        {
            return handledExitCode.Value;
        }

        var parsedArgs = parsed ?? throw new InvalidOperationException("Parsed args were not available.");

        if (parsedArgs.Stdin || parsedArgs.Paths.Any(p => p == "-"))
        {
            if (parsedArgs.DetectEncoding)
            {
                var decoded = await CharsetDetection.ReadStreamAsync(stdin);
                using var decodedReader = new StringReader(decoded.Text);
                return await RunParsedAsync(parsedArgs, decodedReader, stdout, stderr);
            }

            using var streamReader = new StreamReader(
                stdin,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

            return await RunParsedAsync(parsedArgs, streamReader, stdout, stderr);
        }

        return await RunParsedAsync(parsedArgs, TextReader.Null, stdout, stderr);
    }

    private static async Task<(CliArgs? Parsed, int? HandledExitCode)> ParseOrHandleBuiltInAsync(
        string[] args,
        TextWriter stdout)
    {
        EncodingProviderRegistry.EnsureRegistered();

        if (CliParser.IsHelpOrVersionRequest(args))
        {
            return (null, await CliParser.InvokeAsync(args, stdout));
        }

        return (CliParser.Parse(args), null);
    }

    private static async Task<int> RunParsedAsync(CliArgs parsed, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        // Default to lint when no subcommand is specified
        var command = parsed.IsExplicitCommand ? parsed.Command : "lint";

        try
        {
            ValidateOptions(parsed);

            // Initialize services
            var inputReader = new InputReader();
            var commandExecutor = new CommandExecutor(inputReader);

            return command switch
            {
                "init" => await CommandExecutor.ExecuteInitAsync(parsed, stdout, stderr),
                "print-config" => await CommandExecutor.ExecutePrintConfigAsync(parsed, stdout, stderr),
                "print-format-config" => await CommandExecutor.ExecutePrintFormatConfigAsync(parsed, stdout, stderr),
                "list-rules" => await CommandExecutor.ExecuteListRulesAsync(parsed, stdout, stderr),
                "list-plugins" => await CommandExecutor.ExecuteListPluginsAsync(parsed, stdout, stderr),
                "format" => await commandExecutor.ExecuteFormatAsync(parsed, stdin, stdout, stderr),
                "fix" => await commandExecutor.ExecuteFixAsync(parsed, stdin, stdout, stderr),
                "lint" => await commandExecutor.ExecuteLintAsync("lint", parsed, stdin, stdout, stderr),
                _ => await UnknownCommandAsync(command, stderr)
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

    private static void ValidateOptions(CliArgs args)
    {
        if (args.Preset is not null && args.RulesetPath is not null)
        {
            throw new ConfigException(
                "--preset and --ruleset are mutually exclusive. Use one or the other.");
        }
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter stderr)
    {
        await stderr.WriteLineAsync($"Unknown command: '{command}'. Run 'tsqlrefine --help' for available commands.");
        return ExitCodes.Fatal;
    }
}
