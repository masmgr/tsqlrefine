using System.CommandLine;
using System.CommandLine.Parsing;
using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public static class CliParser
{
    // =================================================================
    // Global Options (available on all commands via RootCommand)
    // These use Recursive = true so they're available on all subcommands
    // =================================================================
    private static readonly Option<string?> ConfigOption = new("--config", "-c")
    {
        Description = "Configuration file path",
        Arity = ArgumentArity.ZeroOrOne,
        Recursive = true
    };

    private static readonly Option<bool> HelpOption = new("--help", "-h", "/?")
    {
        Description = "Show help information"
    };

    private static readonly Option<bool> VersionOption = new("--version", "-v")
    {
        Description = "Show version information"
    };

    // =================================================================
    // Shared Options (added to specific commands as needed)
    // =================================================================

    // Input options
    private static Option<string?> CreateIgnoreListOption() => new("--ignorelist", "-g")
    {
        Description = "Ignore patterns file",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Option<bool> CreateDetectEncodingOption() => new("--detect-encoding")
    {
        Description = "Auto-detect file encoding"
    };

    private static Option<bool> CreateStdinOption() => new("--stdin")
    {
        Description = "Read from stdin"
    };

    private static Option<string?> CreateStdinFilePathOption() => new("--stdin-filepath")
    {
        Description = "Set filepath for stdin input",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Argument<string[]> CreatePathsArgument() => new("paths")
    {
        Description = "SQL files to process",
        Arity = ArgumentArity.ZeroOrMore
    };

    // Output options
    private static Option<string?> CreateOutputOption() => new("--output")
    {
        Description = "Output format (text/json)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Option<bool> CreateWriteOption() => new("--write")
    {
        Description = "Apply changes in-place"
    };

    private static Option<bool> CreateDiffOption() => new("--diff")
    {
        Description = "Show diff output"
    };

    // Analysis options
    private static Option<string?> CreateCompatLevelOption() => new("--compat-level")
    {
        Description = "SQL Server compatibility level (100-160)",
        Arity = ArgumentArity.ZeroOrOne
    };

    // Rule options
    private static Option<string?> CreateSeverityOption() => new("--severity")
    {
        Description = "Minimum severity level (error/warning/info/hint)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Option<string?> CreatePresetOption() => new("--preset")
    {
        Description = "Preset ruleset (recommended/strict/pragmatic/security-only)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Option<string?> CreateRulesetOption() => new("--ruleset")
    {
        Description = "Custom ruleset file path",
        Arity = ArgumentArity.ZeroOrOne
    };

    // Format options
    private static Option<string?> CreateIndentStyleOption() => new("--indent-style")
    {
        Description = "Indentation style (tabs/spaces)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static Option<string?> CreateIndentSizeOption() => new("--indent-size")
    {
        Description = "Indentation size in spaces",
        Arity = ArgumentArity.ZeroOrOne
    };

    // List-plugins options
    private static Option<bool> CreateVerboseOption() => new("--verbose")
    {
        Description = "Show detailed information"
    };

    // =================================================================
    // Command Builders
    // =================================================================

    private static Command BuildLintCommand()
    {
        var command = new Command("lint", "Analyze SQL files for rule violations");

        // Input options
        command.Options.Add(CreateIgnoreListOption());
        command.Options.Add(CreateDetectEncodingOption());
        command.Options.Add(CreateStdinOption());
        command.Options.Add(CreateStdinFilePathOption());

        // Output options
        command.Options.Add(CreateOutputOption());

        // Analysis options
        command.Options.Add(CreateCompatLevelOption());

        // Rule options
        command.Options.Add(CreateSeverityOption());
        command.Options.Add(CreatePresetOption());
        command.Options.Add(CreateRulesetOption());

        // Paths argument
        command.Arguments.Add(CreatePathsArgument());

        return command;
    }

    private static Command BuildFormatCommand()
    {
        var command = new Command("format", "Format SQL files (keyword casing, whitespace)");

        // Input options
        command.Options.Add(CreateIgnoreListOption());
        command.Options.Add(CreateDetectEncodingOption());
        command.Options.Add(CreateStdinOption());
        command.Options.Add(CreateStdinFilePathOption());

        // Output options
        command.Options.Add(CreateOutputOption());
        command.Options.Add(CreateWriteOption());
        command.Options.Add(CreateDiffOption());

        // Analysis options
        command.Options.Add(CreateCompatLevelOption());

        // Format options
        command.Options.Add(CreateIndentStyleOption());
        command.Options.Add(CreateIndentSizeOption());

        // Paths argument
        command.Arguments.Add(CreatePathsArgument());

        return command;
    }

    private static Command BuildFixCommand()
    {
        var command = new Command("fix", "Auto-fix issues that support fixing");

        // Input options
        command.Options.Add(CreateIgnoreListOption());
        command.Options.Add(CreateDetectEncodingOption());
        command.Options.Add(CreateStdinOption());
        command.Options.Add(CreateStdinFilePathOption());

        // Output options
        command.Options.Add(CreateOutputOption());
        command.Options.Add(CreateWriteOption());
        command.Options.Add(CreateDiffOption());

        // Analysis options
        command.Options.Add(CreateCompatLevelOption());

        // Rule options
        command.Options.Add(CreateSeverityOption());
        command.Options.Add(CreatePresetOption());
        command.Options.Add(CreateRulesetOption());

        // Paths argument
        command.Arguments.Add(CreatePathsArgument());

        return command;
    }

    private static Command BuildInitCommand()
    {
        return new Command("init", "Initialize configuration files");
    }

    private static Command BuildPrintConfigCommand()
    {
        var command = new Command("print-config", "Print effective configuration");

        // Output options
        command.Options.Add(CreateOutputOption());

        return command;
    }

    private static Command BuildListRulesCommand()
    {
        var command = new Command("list-rules", "List available rules");

        // Output options
        command.Options.Add(CreateOutputOption());

        return command;
    }

    private static Command BuildListPluginsCommand()
    {
        var command = new Command("list-plugins", "List loaded plugins");

        // Output options
        command.Options.Add(CreateOutputOption());
        command.Options.Add(CreateVerboseOption());

        return command;
    }

    // =================================================================
    // Root Command
    // =================================================================

    private static readonly RootCommand Root = BuildRootCommand();

    private static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("A SQL Server/T-SQL linter, static analyzer, and formatter");

        // Global options
        root.Options.Add(ConfigOption);
        root.Options.Add(HelpOption);
        root.Options.Add(VersionOption);

        // Subcommands
        root.Subcommands.Add(BuildLintCommand());
        root.Subcommands.Add(BuildFormatCommand());
        root.Subcommands.Add(BuildFixCommand());
        root.Subcommands.Add(BuildInitCommand());
        root.Subcommands.Add(BuildPrintConfigCommand());
        root.Subcommands.Add(BuildListRulesCommand());
        root.Subcommands.Add(BuildListPluginsCommand());

        return root;
    }

    // =================================================================
    // Parse
    // =================================================================

    public static CliArgs Parse(string[] args)
    {
        var parseResult = Root.Parse(args ?? []);
        var command = GetCommandName(parseResult);

        return new CliArgs(
            Command: command,
            ShowHelp: HasBoolOption(parseResult, "--help", "-h", "/?"),
            ShowVersion: HasBoolOption(parseResult, "--version", "-v"),
            ConfigPath: GetOptionValue<string?>(parseResult, "--config"),
            IgnoreListPath: GetOptionValue<string?>(parseResult, "--ignorelist"),
            DetectEncoding: GetOptionValue<bool>(parseResult, "--detect-encoding"),
            Stdin: GetOptionValue<bool>(parseResult, "--stdin"),
            StdinFilePath: GetOptionValue<string?>(parseResult, "--stdin-filepath"),
            Output: GetOptionValue<string?>(parseResult, "--output") ?? "text",
            MinimumSeverity: ParseSeverity(GetOptionValue<string?>(parseResult, "--severity")),
            Preset: GetOptionValue<string?>(parseResult, "--preset"),
            CompatLevel: ParseInt(GetOptionValue<string?>(parseResult, "--compat-level")),
            RulesetPath: GetOptionValue<string?>(parseResult, "--ruleset"),
            Write: GetOptionValue<bool>(parseResult, "--write"),
            Diff: GetOptionValue<bool>(parseResult, "--diff"),
            IndentStyle: ParseIndentStyle(GetOptionValue<string?>(parseResult, "--indent-style")),
            IndentSize: ParseInt(GetOptionValue<string?>(parseResult, "--indent-size")),
            Verbose: GetOptionValue<bool>(parseResult, "--verbose"),
            Paths: GetPaths(parseResult)
        );
    }

    private static string GetCommandName(ParseResult parseResult)
    {
        // Default to "lint" if no subcommand specified
        return parseResult.CommandResult.Command is RootCommand
            ? "lint"
            : parseResult.CommandResult.Command.Name;
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

    private static bool HasBoolOption(ParseResult parseResult, params string[] names)
    {
        var namesSet = new HashSet<string>(names, StringComparer.Ordinal);
        return parseResult.Tokens.Any(t => namesSet.Contains(t.Value));
    }
}
