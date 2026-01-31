using System.Reflection;
using System.Text;
using TsqlRefine.Cli.Services;
using TsqlRefine.Core;

namespace TsqlRefine.Cli;

public static class CliApp
{
    private static string GetVersionString()
    {
        var assembly = typeof(CliApp).Assembly;

        // Try InformationalVersion first (includes pre-release tags)
        var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersionAttr?.InformationalVersion is not null)
            return infoVersionAttr.InformationalVersion;

        // Fallback to AssemblyVersion
        var version = assembly.GetName().Version;
        if (version is not null)
            return version.ToString();

        return "unknown";
    }

    public static async Task<int> RunAsync(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        EncodingProviderRegistry.EnsureRegistered();
        var parsed = CliParser.Parse(args);
        return await RunParsedAsync(parsed, stdin, stdout, stderr);
    }

    public static async Task<int> RunAsync(string[] args, Stream stdin, TextWriter stdout, TextWriter stderr)
    {
        EncodingProviderRegistry.EnsureRegistered();
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
        EncodingProviderRegistry.EnsureRegistered();

        if (parsed.ShowVersion)
        {
            await stdout.WriteLineAsync($"tsqlrefine {GetVersionString()}");
            return 0;
        }

        if (parsed.ShowHelp)
        {
            // Show root help if no explicit command, otherwise show command-specific help
            if (parsed.IsExplicitCommand)
            {
                var commandHelp = CliParser.GetCommandHelp(parsed.Command);
                await stdout.WriteLineAsync(commandHelp ?? HelpText);
            }
            else
            {
                await stdout.WriteLineAsync(HelpText);
            }

            return 0;
        }

        // Require explicit subcommand
        if (!parsed.IsExplicitCommand)
        {
            await stderr.WriteLineAsync("Error: No command specified.");
            await stderr.WriteLineAsync();
            await stderr.WriteLineAsync(HelpText);
            return ExitCodes.Fatal;
        }

        try
        {
            // Initialize services
            var configLoader = new ConfigLoader();
            var inputReader = new InputReader();
            var formattingOptionsResolver = new FormattingOptionsResolver(configLoader);
            var outputWriter = new OutputWriter();
            var pluginDiagnostics = new PluginDiagnostics();
            var commandExecutor = new CommandExecutor(
                configLoader,
                inputReader,
                formattingOptionsResolver,
                outputWriter,
                pluginDiagnostics);

            return parsed.Command switch
            {
                "init" => await commandExecutor.ExecuteInitAsync(parsed, stdout, stderr),
                "print-config" => await commandExecutor.ExecutePrintConfigAsync(parsed, stdout, stderr),
                "list-rules" => await commandExecutor.ExecuteListRulesAsync(parsed, stdout, stderr),
                "list-plugins" => await commandExecutor.ExecuteListPluginsAsync(parsed, stdout, stderr),
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
        catch (Exception ex)
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


    private const string HelpText =
        """
        tsqlrefine - A SQL Server/T-SQL linter, static analyzer, and formatter

        Usage:
          tsqlrefine <command> [options] [paths...]

        Commands:
          lint          Analyze SQL files for rule violations
          format        Format SQL files (keyword casing, whitespace)
          fix           Auto-fix issues that support fixing
          init          Initialize configuration files
          print-config  Print effective configuration
          list-rules    List available rules
          list-plugins  List loaded plugins

        Global Options:
          -c, --config <path>       Configuration file path
          -h, --help                Show help information
          -v, --version             Show version information

        Run 'tsqlrefine <command> --help' for more information on a command.
        """;
}
