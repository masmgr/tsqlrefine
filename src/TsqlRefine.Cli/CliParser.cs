using System.Collections.Frozen;
using System.CommandLine;
using System.CommandLine.Parsing;
using TsqlRefine.Core.Config;
using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

/// <summary>
/// Parses command-line arguments using System.CommandLine.
/// </summary>
public static class CliParser
{
    public const string ConnectionStringEnvironmentVariable = "TSQLREFINE_CONNECTION_STRING";

    private static readonly FrozenSet<string> HelpVersionTokens = FrozenSet.ToFrozenSet(
    [
        "--help", "-h", "-?", "/?",
        "--version"
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> OutputFormatCommands = FrozenSet.ToFrozenSet(
    [
        "lint",
        "fix",
        "list-rules",
        "list-plugins",
        "print-format-config"
    ], StringComparer.Ordinal);

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
        public static readonly Option<string?> IgnoreList = new("--ignorelist")
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

        public static readonly Option<bool> Utf8 = new("--utf8")
        {
            Description = "Set console encoding to UTF-8 (stdin and stdout)",
            Recursive = true
        };

        // Output options
        public static readonly Option<string?> Output = new("--output")
        {
            Description = "Output format (text/json/sarif for lint)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> ReportOutputFormat = new("--output-format")
        {
            Description = "Report output format (json/html)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> ReportOutput = new("--output")
        {
            Description = "Report output file path (writes to stdout when omitted)",
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string?> Baseline = new("--baseline")
        {
            Description = "Baseline JSON file path",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> ShowSuppressed = new("--show-suppressed")
        {
            Description = "Include baseline-suppressed diagnostics in output"
        };

        public static readonly Option<string?> BaselineRoot = new("--root")
        {
            Description = "Root directory for baseline path normalization",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> RemoveMissing = new("--remove-missing")
        {
            Description = "Remove baseline entries for files that no longer exist"
        };

        public static readonly Option<string?> BaselineOutput = new("--output")
        {
            Description = "Baseline JSON output file path",
            Arity = ArgumentArity.ExactlyOne
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
            Description = "Custom ruleset name or file path",
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

        // Init options
        public static readonly Option<bool> Force = new("--force")
        {
            Description = "Overwrite existing configuration files"
        };

        public static readonly Option<bool> Global = new("--global")
        {
            Description = "Create configuration in home directory (~/.tsqlrefine/)"
        };

        // List-rules filter options
        public static readonly Option<string?> Category = new("--category")
        {
            Description = "Filter rules by category",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> Fixable = new("--fixable")
        {
            Description = "Show only fixable rules"
        };

        public static readonly Option<bool> EnabledOnly = new("--enabled-only")
        {
            Description = "Show only enabled rules"
        };

        // Misc options
        public static readonly Option<bool> Verbose = new("--verbose")
        {
            Description = "Show detailed information"
        };

        public static readonly Option<bool> Quiet = new("--quiet", "-q")
        {
            Description = "Suppress informational stderr output (for IDE/extension integration)"
        };

        public static readonly Option<bool> ShowSources = new("--show-sources")
        {
            Description = "Show where each option value originated"
        };

        public static readonly Option<string?> MaxFileSize = new("--max-file-size")
        {
            Description = "Maximum file size in MB (default: 10)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> AllowPlugins = new("--allow-plugins")
        {
            Description = "Enable loading of plugin DLLs from configuration",
            Recursive = true
        };

        // Schema options
        public static readonly Option<string?> Schema = new("--schema")
        {
            Description = "Schema snapshot file path for schema-aware analysis",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> RelationsProfile = new("--relations-profile")
        {
            Description = "Relations profile file path for JOIN pattern deviation analysis",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> ObjectsCatalog = new("--objects-catalog")
        {
            Description = "Object catalog file path for cross-object analysis",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Schema snapshot options
        public static readonly Option<string?> ConnectionString = new("--connection-string")
        {
            Description = $"SQL Server connection string (or set {ConnectionStringEnvironmentVariable})",
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string?> SchemaOutput = new("--output")
        {
            Description = "Output file path for schema snapshot",
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string?> IncludeSchema = new("--include-schema")
        {
            Description = "Comma-separated schema names to include",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> ExcludeSchema = new("--exclude-schema")
        {
            Description = "Comma-separated schema names to exclude",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> SchemaOutputDir = new("--output-dir")
        {
            Description = "Output directory for schema.json, relations.json, and objects.json",
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string?> SchemaRelationsOutput = new("--relations-output")
        {
            Description = "Output path for relations profile (overrides --output-dir for relations.json)",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<string?> SchemaObjectsOutput = new("--objects-output")
        {
            Description = "Output path for object catalog (overrides --output-dir for objects.json)",
            Arity = ArgumentArity.ZeroOrOne
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
        command.Options.Add(Options.MaxFileSize);
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

    private static Command WithSchemaOption(this Command command)
    {
        command.Options.Add(Options.Schema);
        command.Options.Add(Options.RelationsProfile);
        command.Options.Add(Options.ObjectsCatalog);
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
            .WithSchemaOption()
            .WithPathsArgument();
        command.Options.Add(Options.Verbose);
        command.Options.Add(Options.Quiet);
        command.Options.Add(Options.Baseline);
        command.Options.Add(Options.BaselineRoot);
        command.Options.Add(Options.ShowSuppressed);
        return command;
    }

    private static Command BuildFormatCommand()
    {
        var command = new Command("format", "Format SQL files (keyword casing, whitespace)")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithFormatOptions()
            .WithPathsArgument();
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildFixCommand()
    {
        var command = new Command("fix", "Auto-fix issues that support fixing")
            .WithInputOptions()
            .WithOutputOption()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithRuleIdOption()
            .WithSchemaOption()
            .WithPathsArgument();
        command.Options.Add(Options.Verbose);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildInitCommand()
    {
        var command = new Command("init", "Initialize configuration files");
        command.Options.Add(Options.Force);
        command.Options.Add(Options.Global);
        command.Options.Add(Options.Preset);
        command.Options.Add(Options.CompatLevel);
        return command;
    }

    private static Command BuildPrintConfigCommand() =>
        new Command("print-config", "Print effective configuration");

    private static Command BuildListRulesCommand()
    {
        var command = new Command("list-rules", "List available rules")
            .WithOutputOption();
        command.Options.Add(Options.Category);
        command.Options.Add(Options.Fixable);
        command.Options.Add(Options.EnabledOnly);
        command.Options.Add(Options.Preset);
        command.Options.Add(Options.Ruleset);
        return command;
    }

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

    private static Command BuildSchemaCommand()
    {
        var schemaCommand = new Command("schema", "Schema management commands");
        schemaCommand.Subcommands.Add(BuildSchemaSnapshotCommand());
        schemaCommand.Subcommands.Add(BuildSchemaCollectRelationsCommand());
        schemaCommand.Subcommands.Add(BuildSchemaCollectObjectsCommand());
        schemaCommand.Subcommands.Add(BuildSchemaBuildCommand());
        return schemaCommand;
    }

    private static Command BuildBaselineCommand()
    {
        var baselineCommand = new Command("baseline", "Create and maintain diagnostic baselines");
        baselineCommand.Subcommands.Add(BuildBaselineCreateCommand());
        baselineCommand.Subcommands.Add(BuildBaselineTrimCommand());
        return baselineCommand;
    }

    private static Command BuildReportCommand()
    {
        var command = new Command("report", "Generate a diagnostics and SQL metrics report")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithSchemaOption()
            .WithPathsArgument();
        command.Options.Add(Options.ReportOutputFormat);
        command.Options.Add(Options.ReportOutput);
        command.Options.Add(Options.Baseline);
        command.Options.Add(Options.BaselineRoot);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildBaselineCreateCommand()
    {
        var command = new Command("create", "Create a baseline from current diagnostics")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithSchemaOption()
            .WithPathsArgument();
        command.Options.Add(Options.BaselineOutput);
        command.Options.Add(Options.BaselineRoot);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildBaselineTrimCommand()
    {
        var command = new Command("trim", "Remove resolved diagnostics from a baseline")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithRuleOptions()
            .WithSchemaOption()
            .WithPathsArgument();
        command.Options.Add(Options.Baseline);
        command.Options.Add(Options.BaselineRoot);
        command.Options.Add(Options.RemoveMissing);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildSchemaBuildCommand()
    {
        var command = new Command("build", "Generate schema snapshot, JOIN relations, and object catalog in one step")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithPathsArgument();
        command.Options.Add(Options.ConnectionString);
        command.Options.Add(Options.SchemaOutputDir);
        command.Options.Add(Options.SchemaRelationsOutput);
        command.Options.Add(Options.SchemaObjectsOutput);
        command.Options.Add(Options.IncludeSchema);
        command.Options.Add(Options.ExcludeSchema);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildSchemaCollectRelationsCommand()
    {
        var command = new Command("collect-relations", "Collect JOIN relation patterns from SQL files")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithPathsArgument();
        command.Options.Add(Options.SchemaOutput);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildSchemaCollectObjectsCommand()
    {
        var command = new Command("collect-objects", "Collect SQL object definitions and references from SQL files")
            .WithInputOptions()
            .WithCompatLevelOption()
            .WithPathsArgument();
        command.Options.Add(Options.SchemaOutput);
        command.Options.Add(Options.Quiet);
        return command;
    }

    private static Command BuildSchemaSnapshotCommand()
    {
        var command = new Command("snapshot", "Generate a schema snapshot from a database");
        command.Options.Add(Options.ConnectionString);
        command.Options.Add(Options.SchemaOutput);
        command.Options.Add(Options.IncludeSchema);
        command.Options.Add(Options.ExcludeSchema);
        command.Options.Add(Options.CompatLevel);
        command.Options.Add(Options.Quiet);
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
        root.Options.Add(Options.Utf8);
        root.Options.Add(Options.AllowPlugins);

        // Subcommands
        root.Subcommands.Add(BuildLintCommand());
        root.Subcommands.Add(BuildFormatCommand());
        root.Subcommands.Add(BuildFixCommand());
        root.Subcommands.Add(BuildInitCommand());
        root.Subcommands.Add(BuildPrintConfigCommand());
        root.Subcommands.Add(BuildPrintFormatConfigCommand());
        root.Subcommands.Add(BuildListRulesCommand());
        root.Subcommands.Add(BuildListPluginsCommand());
        root.Subcommands.Add(BuildSchemaCommand());
        root.Subcommands.Add(BuildBaselineCommand());
        root.Subcommands.Add(BuildReportCommand());

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
        return args.Any(static arg => HelpVersionTokens.Contains(arg));
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

        if (parseResult.Errors.Count > 0 && parseResult.CommandResult.Command is not RootCommand)
        {
            throw new ConfigException(string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)));
        }

        return new CliArgs(
            Command: command,
            IsExplicitCommand: isExplicit,
            ConfigPath: GetOptionValue<string?>(parseResult, "--config"),
            IgnoreListPath: GetOptionValue<string?>(parseResult, "--ignorelist"),
            DetectEncoding: GetOptionValue<bool>(parseResult, "--detect-encoding"),
            Stdin: GetOptionValue<bool>(parseResult, "--stdin"),
            Utf8: GetOptionValue<bool>(parseResult, "--utf8"),
            Output: command == "report"
                ? "text"
                : ParseOutput(command, GetOptionValue<string?>(parseResult, "--output")),
            MinimumSeverity: ParseSeverity(GetOptionValue<string?>(parseResult, "--severity")),
            Preset: GetOptionValue<string?>(parseResult, "--preset"),
            CompatLevel: ParseCompatLevel(GetOptionValue<string?>(parseResult, "--compat-level")),
            RulesetPath: GetOptionValue<string?>(parseResult, "--ruleset"),
            IndentStyle: ParseIndentStyle(GetOptionValue<string?>(parseResult, "--indent-style")),
            IndentSize: ParsePositiveInt(GetOptionValue<string?>(parseResult, "--indent-size"), "--indent-size"),
            LineEnding: ParseLineEnding(GetOptionValue<string?>(parseResult, "--line-ending")),
            Verbose: GetOptionValue<bool>(parseResult, "--verbose"),
            Quiet: GetOptionValue<bool>(parseResult, "--quiet"),
            ShowSources: GetOptionValue<bool>(parseResult, "--show-sources"),
            Force: GetOptionValue<bool>(parseResult, "--force"),
            Global: GetOptionValue<bool>(parseResult, "--global"),
            Category: GetOptionValue<string?>(parseResult, "--category"),
            FixableOnly: GetOptionValue<bool>(parseResult, "--fixable"),
            EnabledOnly: GetOptionValue<bool>(parseResult, "--enabled-only"),
            Paths: ValidatePathTokens(GetPaths(parseResult)),
            RuleId: GetOptionValue<string?>(parseResult, "--rule"),
            MaxFileSize: ParseMaxFileSize(GetOptionValue<string?>(parseResult, "--max-file-size")),
            AllowPlugins: GetOptionValue<bool>(parseResult, "--allow-plugins"),
            SchemaPath: GetOptionValue<string?>(parseResult, "--schema"),
            RelationsProfilePath: GetOptionValue<string?>(parseResult, "--relations-profile"),
            ObjectsCatalogPath: GetOptionValue<string?>(parseResult, "--objects-catalog"),
            SchemaConnectionString: ResolveSchemaConnectionString(
                GetOptionValue<string?>(parseResult, "--connection-string")),
            SchemaOutput: GetSchemaOutput(parseResult),
            SchemaIncludeSchemas: GetOptionValue<string?>(parseResult, "--include-schema"),
            SchemaExcludeSchemas: GetOptionValue<string?>(parseResult, "--exclude-schema"),
            SchemaOutputDir: GetOptionValue<string?>(parseResult, "--output-dir"),
            SchemaRelationsOutput: GetOptionValue<string?>(parseResult, "--relations-output"),
            SchemaObjectsOutput: GetOptionValue<string?>(parseResult, "--objects-output"),
            BaselinePath: GetOptionValue<string?>(parseResult, "--baseline"),
            BaselineOutput: GetBaselineOutput(parseResult),
            BaselineRoot: GetOptionValue<string?>(parseResult, "--root"),
            ShowSuppressed: GetOptionValue<bool>(parseResult, "--show-suppressed"),
            RemoveMissing: GetOptionValue<bool>(parseResult, "--remove-missing"),
            ReportOutputFormat: ParseReportOutputFormat(
                GetOptionValue<string?>(parseResult, "--output-format")),
            ReportOutputPath: GetReportOutput(parseResult)
        );
    }

    // =================================================================
    // Parse Helpers
    // =================================================================

    private static string? ResolveSchemaConnectionString(string? commandLineValue)
        => !string.IsNullOrWhiteSpace(commandLineValue)
            ? commandLineValue
            : Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

    private static (string Command, bool IsExplicit) GetCommandName(ParseResult parseResult)
    {
        // Require explicit subcommand
        if (parseResult.CommandResult.Command is RootCommand)
        {
            return ("", false);
        }

        // Handle nested commands (e.g., "schema snapshot" → "schema snapshot")
        var parts = new List<string>();
        var current = parseResult.CommandResult;
        while (current is not null && current.Command is not RootCommand)
        {
            parts.Add(current.Command.Name);
            current = current.Parent as CommandResult;
        }

        parts.Reverse();
        return (string.Join(" ", parts), true);
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

    private static List<string> ValidatePathTokens(List<string> paths)
    {
        foreach (var path in paths)
        {
            if (path != "-" && path.Length > 0 && path[0] == '-')
            {
                throw new ConfigException(
                    $"Unrecognized option or invalid path token: '{path}'. If this is a file path, prefix it with .\\ or an absolute path.");
            }
        }

        return paths;
    }

    private static string? GetSchemaOutput(ParseResult parseResult)
    {
        // Schema snapshot uses its own --output option (Options.SchemaOutput)
        var commandResult = parseResult.CommandResult;
        var option = commandResult.Command.Options.FirstOrDefault(o => o.Name == "--output");
        if (option is Option<string?> typedOption && option == Options.SchemaOutput)
        {
            var optionResult = parseResult.GetResult(typedOption);
            if (optionResult is not null)
            {
                return parseResult.GetValue(typedOption);
            }
        }

        return null;
    }

    private static string? GetBaselineOutput(ParseResult parseResult)
    {
        var commandResult = parseResult.CommandResult;
        var option = commandResult.Command.Options.FirstOrDefault(o => o.Name == "--output");
        if (option is Option<string?> typedOption && option == Options.BaselineOutput)
        {
            return parseResult.GetValue(typedOption);
        }

        return null;
    }

    private static string? GetReportOutput(ParseResult parseResult)
    {
        var commandResult = parseResult.CommandResult;
        var option = commandResult.Command.Options.FirstOrDefault(o => o.Name == "--output");
        if (option is Option<string?> typedOption && option == Options.ReportOutput)
        {
            return parseResult.GetValue(typedOption);
        }

        return null;
    }

    private static string ParseReportOutputFormat(string? value) => value?.ToLowerInvariant() switch
    {
        null or "json" => "json",
        "html" => "html",
        _ => throw new ConfigException(
            $"Invalid --output-format value: '{value}'. Expected one of: json, html.")
    };

    private static string ParseOutput(string command, string? value)
    {
        if (!OutputFormatCommands.Contains(command))
        {
            return "text";
        }

        if (value is null)
        {
            return "text";
        }

        return value.ToLowerInvariant() switch
        {
            "text" => "text",
            "json" => "json",
            "sarif" when command == "lint" => "sarif",
            _ => throw new ConfigException(
                $"Invalid --output value: '{value}'. Expected one of: " +
                (command == "lint" ? "text, json, sarif." : "text, json."))
        };
    }

    private const long DefaultMaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB

    private static long ParseMaxFileSize(string? s)
    {
        if (s is null)
            return DefaultMaxFileSizeBytes;
        if (int.TryParse(s, out var mb) && mb > 0)
            return (long)mb * 1024 * 1024;
        throw new ConfigException(
            $"Invalid --max-file-size value: '{s}'. Expected a positive integer (MB).");
    }

    private static int? ParsePositiveInt(string? s, string optionName)
    {
        if (s is null)
        {
            return null;
        }

        if (int.TryParse(s, out var value) && value > 0)
        {
            return value;
        }

        throw new ConfigException($"Invalid {optionName} value: '{s}'. Expected a positive integer.");
    }

    private static int? ParseCompatLevel(string? s)
    {
        if (s is null)
        {
            return null;
        }

        if (!int.TryParse(s, out var value))
        {
            throw new ConfigException($"Invalid --compat-level value: '{s}'. Expected one of: {FormatCompatLevels()}.");
        }

        if (!TsqlRefineConfig.ValidCompatLevels.Contains(value))
        {
            throw new ConfigException($"Invalid --compat-level value: '{s}'. Expected one of: {FormatCompatLevels()}.");
        }

        return value;
    }

    private static DiagnosticSeverity? ParseSeverity(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Information,
            "hint" => DiagnosticSeverity.Hint,
            null => null,
            _ => throw new ConfigException(
                $"Invalid --severity value: '{s}'. Expected one of: error, warning, info, hint.")
        };

    private static IndentStyle? ParseIndentStyle(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "tabs" => IndentStyle.Tabs,
            "spaces" => IndentStyle.Spaces,
            null => null,
            _ => throw new ConfigException(
                $"Invalid --indent-style value: '{s}'. Expected one of: tabs, spaces.")
        };

    private static LineEnding? ParseLineEnding(string? s) =>
        s?.ToLowerInvariant() switch
        {
            "auto" => LineEnding.Auto,
            "lf" => LineEnding.Lf,
            "crlf" => LineEnding.CrLf,
            null => null,
            _ => throw new ConfigException(
                $"Invalid --line-ending value: '{s}'. Expected one of: auto, lf, crlf.")
        };

    private static string FormatCompatLevels() =>
        string.Join(", ", TsqlRefineConfig.ValidCompatLevels.OrderBy(x => x));
}
