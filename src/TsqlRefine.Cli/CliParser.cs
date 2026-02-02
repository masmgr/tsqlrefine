using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public static class CliParser
{
    // =================================================================
    // Option Definitions
    // =================================================================

    private static class Options
    {
        // Global options (Recursive = true for all subcommands)
        public static readonly Option<string?> Config = new("--config", "-c")
        {
            Description = "Configuration file path",
            Arity = ArgumentArity.ZeroOrOne,
            Recursive = true
        };

        // Input options
        public static readonly Option<string?> IgnoreList = new("--ignorelist", "-g")
        {
            Description = "Ignore patterns file",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> DetectEncoding = new("--detect-encoding")
        {
            Description = "Auto-detect file encoding"
        };

        public static readonly Option<bool> Stdin = new("--stdin")
        {
            Description = "Read from stdin"
        };

        // Output options
        public static readonly Option<string?> Output = new("--output")
        {
            Description = "Output format (text/json)",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Analysis options
        public static readonly Option<string?> CompatLevel = new("--compat-level")
        {
            Description = "SQL Server compatibility level (100-160)",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Rule options
        public static readonly Option<string?> Severity = new("--severity")
        {
            Description = "Minimum severity level (error/warning/info/hint)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> Preset = new("--preset")
        {
            Description = "Preset ruleset (recommended/strict/pragmatic/security-only)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> Ruleset = new("--ruleset")
        {
            Description = "Custom ruleset file path",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> Rule = new("--rule")
        {
            Description = "Rule ID to apply",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Format options
        public static readonly Option<string?> IndentStyle = new("--indent-style")
        {
            Description = "Indentation style (tabs/spaces)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> IndentSize = new("--indent-size")
        {
            Description = "Indentation size in spaces",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> LineEnding = new("--line-ending")
        {
            Description = "Line ending style (auto/lf/crlf)",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Misc options
        public static readonly Option<bool> Verbose = new("--verbose")
        {
            Description = "Show detailed information"
        };

        public static readonly Option<bool> ShowSources = new("--show-sources")
        {
            Description = "Show where each option value originated"
        };

        // Arguments (factory method because each command needs its own instance)
        public static Argument<string[]> CreatePathsArgument() => new("paths")
        {
            Description = "SQL files to process",
            Arity = ArgumentArity.ZeroOrMore
        };
    }

    // =================================================================
    // Command Extension Methods
    // =================================================================

    private static Command WithInputOptions(this Command command)
    {
        command.Options.Add(Options.IgnoreList);
        command.Options.Add(Options.DetectEncoding);
        command.Options.Add(Options.Stdin);
        return command;
    }

    private static Command WithOutputOption(this Command command)
    {
        command.Options.Add(Options.Output);
        return command;
    }

    private static Command WithCompatLevelOption(this Command command)
    {
        command.Options.Add(Options.CompatLevel);
        return command;
    }

    private static Command WithRuleOptions(this Command command)
    {
        command.Options.Add(Options.Severity);
        command.Options.Add(Options.Preset);
        command.Options.Add(Options.Ruleset);
        return command;
    }

    private static Command WithRuleIdOption(this Command command)
    {
        command.Options.Add(Options.Rule);
        return command;
    }

    private static Command WithFormatOptions(this Command command)
    {
        command.Options.Add(Options.IndentStyle);
        command.Options.Add(Options.IndentSize);
        command.Options.Add(Options.LineEnding);
        return command;
    }

    private static Command WithPathsArgument(this Command command)
    {
        command.Arguments.Add(Options.CreatePathsArgument());
        return command;
    }

    // =================================================================
    // Command Builders
    // =================================================================

    private static Command BuildLintCommand()
    {
        var command = new Command("lint", "Analyze SQL files for rule violations")
            .WithInputOptions()
            .WithOutputOption()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithPathsArgument();
        command.Options.Add(Options.Verbose);
        return command;
    }

    private static Command BuildFormatCommand() =>
        new Command("format", "Format SQL files (keyword casing, whitespace)")
            .WithInputOptions()
            .WithOutputOption()
            .WithCompatLevelOption()
            .WithFormatOptions()
            .WithPathsArgument();

    private static Command BuildFixCommand() =>
        new Command("fix", "Auto-fix issues that support fixing")
            .WithInputOptions()
            .WithOutputOption()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithRuleIdOption()
            .WithPathsArgument();

    private static Command BuildInitCommand() =>
        new Command("init", "Initialize configuration files");

    private static Command BuildPrintConfigCommand() =>
        new Command("print-config", "Print effective configuration")
            .WithOutputOption();

    private static Command BuildListRulesCommand() =>
        new Command("list-rules", "List available rules")
            .WithOutputOption();

    private static Command BuildListPluginsCommand()
    {
        var command = new Command("list-plugins", "List loaded plugins")
            .WithOutputOption();
        command.Options.Add(Options.Verbose);
        return command;
    }

    private static Command BuildPrintFormatConfigCommand()
    {
        var command = new Command("print-format-config", "Print effective formatting options")
            .WithOutputOption()
            .WithFormatOptions()
            .WithPathsArgument();
        command.Options.Add(Options.ShowSources);
        return command;
    }

    // =================================================================
    // Root Command
    // =================================================================

    private static readonly RootCommand Root = BuildRootCommand();

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("A SQL Server/T-SQL linter, static analyzer, and formatter");

        // Global options (--help and --version are added automatically by System.CommandLine)
        root.Options.Add(Options.Config);

        // Subcommands
        root.Subcommands.Add(BuildLintCommand());
        root.Subcommands.Add(BuildFormatCommand());
        root.Subcommands.Add(BuildFixCommand());
        root.Subcommands.Add(BuildInitCommand());
        root.Subcommands.Add(BuildPrintConfigCommand());
        root.Subcommands.Add(BuildPrintFormatConfigCommand());
        root.Subcommands.Add(BuildListRulesCommand());
        root.Subcommands.Add(BuildListPluginsCommand());

        return root;
    }

    // =================================================================
    // Public API
    // =================================================================

    /// <summary>
    /// Checks if the arguments contain help or version options.
    /// </summary>
    public static bool IsHelpOrVersionRequest(string[] args)
    {
        var helpVersionTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "--help", "-h", "-?", "/?",
            "--version"
        };

        return args.Any(arg => helpVersionTokens.Contains(arg));
    }

    /// <summary>
    /// Invokes the parser to handle help/version output automatically.
    /// Uses System.CommandLine's built-in help/version generation.
    /// </summary>
    public static async Task<int> InvokeAsync(string[] args, TextWriter stdout)
    {
        var parseResult = Root.Parse(args ?? []);
        var config = new InvocationConfiguration
        {
            Output = stdout
        };
        return await parseResult.InvokeAsync(config);
    }

    /// <summary>
    /// Parses command-line arguments into a CliArgs record.
    /// </summary>
    public static CliArgs Parse(string[] args)
    {
        var parseResult = Root.Parse(args ?? []);
        var (command, isExplicit) = GetCommandName(parseResult);

        return new CliArgs(
            Command: command,
            IsExplicitCommand: isExplicit,
            ConfigPath: GetOptionValue<string?>(parseResult, "--config"),
            IgnoreListPath: GetOptionValue<string?>(parseResult, "--ignorelist"),
            DetectEncoding: GetOptionValue<bool>(parseResult, "--detect-encoding"),
            Stdin: GetOptionValue<bool>(parseResult, "--stdin"),
            Output: GetOptionValue<string?>(parseResult, "--output") ?? "text",
            MinimumSeverity: ParseSeverity(GetOptionValue<string?>(parseResult, "--severity")),
            Preset: GetOptionValue<string?>(parseResult, "--preset"),
            CompatLevel: ParseInt(GetOptionValue<string?>(parseResult, "--compat-level")),
            RulesetPath: GetOptionValue<string?>(parseResult, "--ruleset"),
            IndentStyle: ParseIndentStyle(GetOptionValue<string?>(parseResult, "--indent-style")),
            IndentSize: ParseInt(GetOptionValue<string?>(parseResult, "--indent-size")),
            LineEnding: ParseLineEnding(GetOptionValue<string?>(parseResult, "--line-ending")),
            Verbose: GetOptionValue<bool>(parseResult, "--verbose"),
            ShowSources: GetOptionValue<bool>(parseResult, "--show-sources"),
            Paths: GetPaths(parseResult),
            RuleId: GetOptionValue<string?>(parseResult, "--rule")
        );
    }

    // =================================================================
    // Parse Helpers
    // =================================================================

    private static (string Command, bool IsExplicit) GetCommandName(ParseResult parseResult)
    {
        // Require explicit subcommand
        if (parseResult.CommandResult.Command is RootCommand)
        {
            return ("", false);
        }

        return (parseResult.CommandResult.Command.Name, true);
    }

    private static T? GetOptionValue<T>(ParseResult parseResult, string optionName)
    {
        // Search for option in current command and parent commands
        var commandResult = parseResult.CommandResult;
        while (commandResult is not null)
        {
            var option = commandResult.Command.Options.FirstOrDefault(o => o.Name == optionName);
            if (option is Option<T> typedOption)
            {
                var optionResult = parseResult.GetResult(typedOption);
                if (optionResult is not null)
                {
                    return parseResult.GetValue(typedOption);
                }
            }
            commandResult = commandResult.Parent as CommandResult;
        }

        return default;
    }

    private static List<string> GetPaths(ParseResult parseResult)
    {
        var commandResult = parseResult.CommandResult;
        var pathsArg = commandResult.Command.Arguments.FirstOrDefault(a => a.Name == "paths");
        if (pathsArg is Argument<string[]> typedArg)
        {
            var values = parseResult.GetValue(typedArg);
            return values?.ToList() ?? [];
        }

        return [];
    }

    private static int? ParseInt(string? s) =>
        int.TryParse(s, out var value) ? value : null;

    private static DiagnosticSeverity? ParseSeverity(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Information,
            "hint" => DiagnosticSeverity.Hint,
            _ => null
        };

    private static IndentStyle? ParseIndentStyle(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "tabs" => IndentStyle.Tabs,
            "spaces" => IndentStyle.Spaces,
            _ => null
        };

    private static LineEnding? ParseLineEnding(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "auto" => LineEnding.Auto,
            "lf" => LineEnding.Lf,
            "crlf" => LineEnding.CrLf,
            _ => null
        };
}
