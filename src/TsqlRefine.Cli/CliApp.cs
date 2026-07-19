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
        var (parsed, handledExitCode) = await ParseOrHandleBuiltInAsync(args, stdout, stderr);
        if (handledExitCode is not null)
        {
            return handledExitCode.Value;
        }

        return await RunParsedAsync(parsed ?? throw new InvalidOperationException("Parsed args were not available."), stdin, stdout, stderr);
    }

    public static async Task<int> RunAsync(string[] args, Stream stdin, TextWriter stdout, TextWriter stderr)
    {
        var (parsed, handledExitCode) = await ParseOrHandleBuiltInAsync(args, stdout, stderr);
        if (handledExitCode is not null)
        {
            return handledExitCode.Value;
        }

        var parsedArgs = parsed ?? throw new InvalidOperationException("Parsed args were not available.");

        if (parsedArgs.Stdin || parsedArgs.Paths.Any(p => p == "-"))
        {
            if (parsedArgs.DetectEncoding)
            {
                var decoded = await CharsetDetection.ReadStreamAsync(
                    stdin,
                    parsedArgs.MaxFileSize > 0 ? parsedArgs.MaxFileSize : null);
                if (decoded is null)
                {
                    await stderr.WriteLineAsync(
                        $"Stdin input exceeds maximum size of {parsedArgs.MaxFileSize / (1024 * 1024)} MB. Use --max-file-size to increase.");
                    return ExitCodes.Fatal;
                }

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
        TextWriter stdout,
        TextWriter? stderr = null)
    {
        EncodingProviderRegistry.EnsureRegistered();

        if (CliParser.IsHelpOrVersionRequest(args))
        {
            return (null, await CliParser.InvokeAsync(args, stdout));
        }

        try
        {
            return (CliParser.Parse(args), null);
        }
        catch (ConfigException ex)
        {
            if (stderr is not null)
            {
                await stderr.WriteLineAsync(ex.Message);
            }

            return (null, ExitCodes.ConfigError);
        }
    }

    private static async Task<int> RunParsedAsync(CliArgs parsed, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var command = parsed.Command;

        try
        {
            if (!parsed.IsExplicitCommand)
            {
                throw new ConfigException(
                    "A subcommand is required. Run 'tsqlrefine --help' for available commands.");
            }

            ValidateOptions(parsed);
            WarnConflictingOptions(parsed, stderr);

            // Warn about legacy file locations only for commands that use config files
            if (command is "lint" or "format" or "fix" or "report" or "baseline create" or "baseline trim")
            {
                WarnLegacyFileLocations(parsed, stderr);
            }

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
                "report" => await commandExecutor.ExecuteReportAsync(parsed, stdin, stdout, stderr),
                "analyze impact" => await CommandExecutor.ExecuteAnalyzeImpactAsync(parsed, stdout),
                "analyze graph" => await CommandExecutor.ExecuteAnalyzeGraphAsync(parsed, stdout),
                "baseline create" => await commandExecutor.ExecuteBaselineCreateAsync(parsed, stdin, stdout, stderr),
                "baseline trim" => await commandExecutor.ExecuteBaselineTrimAsync(parsed, stdin, stdout, stderr),
                "schema snapshot" => await CommandExecutor.ExecuteSchemaSnapshotAsync(parsed, stdout, stderr),
                "schema collect-relations" => await commandExecutor.ExecuteSchemaCollectRelationsAsync(parsed, stdin, stdout, stderr),
                "schema collect-objects" => await commandExecutor.ExecuteSchemaCollectObjectsAsync(parsed, stdin, stdout, stderr),
                "schema build" => await commandExecutor.ExecuteSchemaBuildAsync(parsed, stdin, stdout, stderr),
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

        if (args.Verbose && args.Quiet)
        {
            throw new ConfigException(
                "--verbose and --quiet are mutually exclusive. Use one or the other.");
        }

        if (args.BaseRef is not null && !args.ChangedOnly)
        {
            throw new ConfigException("--base-ref requires --changed-only.");
        }

        if (args.BaseRef is not null && args.ChangedLinesFrom is not null)
        {
            throw new ConfigException("--base-ref and --changed-lines-from are mutually exclusive.");
        }
    }

    private static void WarnConflictingOptions(CliArgs args, TextWriter stderr)
    {
        if (args.RuleId is null)
        {
            return;
        }

        if (args.Preset is not null)
        {
            stderr.WriteLine("Warning: --rule overrides --preset; preset will be ignored.");
        }

        if (args.RulesetPath is not null)
        {
            stderr.WriteLine("Warning: --rule overrides --ruleset; ruleset will be ignored.");
        }
    }

    private static void WarnLegacyFileLocations(CliArgs args, TextWriter stderr)
    {
        if (args.Quiet)
        {
            return;
        }

        var configWarning = ConfigLoader.CheckLegacyFileWarning("tsqlrefine.json", args.ConfigPath, "--config");
        if (configWarning is not null)
        {
            stderr.WriteLine(configWarning);
        }

        var ignoreWarning = ConfigLoader.CheckLegacyFileWarning("tsqlrefine.ignore", args.IgnoreListPath, "--ignorelist");
        if (ignoreWarning is not null)
        {
            stderr.WriteLine(ignoreWarning);
        }
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter stderr)
    {
        await stderr.WriteLineAsync($"Unknown command: '{command}'. Run 'tsqlrefine --help' for available commands.");
        return ExitCodes.Fatal;
    }
}
