using System.Reflection;
using System.Text;
using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.Formatting;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Services;

public sealed class CommandExecutor
{
    private readonly ConfigLoader _configLoader;
    private readonly InputReader _inputReader;
    private readonly FormattingOptionsResolver _formattingOptionsResolver;
    private readonly OutputWriter _outputWriter;
    private readonly PluginDiagnostics _pluginDiagnostics;

    public CommandExecutor(
        ConfigLoader configLoader,
        InputReader inputReader,
        FormattingOptionsResolver formattingOptionsResolver,
        OutputWriter outputWriter,
        PluginDiagnostics pluginDiagnostics)
    {
        _configLoader = configLoader;
        _inputReader = inputReader;
        _formattingOptionsResolver = formattingOptionsResolver;
        _outputWriter = outputWriter;
        _pluginDiagnostics = pluginDiagnostics;
    }

    public async Task<int> ExecuteInitAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stdout;
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.json");
        var ignorePath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.ignore");

        if (File.Exists(configPath) || File.Exists(ignorePath))
        {
            await stderr.WriteLineAsync("Config files already exist.");
            return ExitCodes.Fatal;
        }

        var config = new TsqlRefineConfig(
            CompatLevel: args.CompatLevel ?? 150,
            Ruleset: "rulesets/recommended.json",
            Plugins: Array.Empty<PluginConfig>()
        );

        await File.WriteAllTextAsync(configPath, System.Text.Json.JsonSerializer.Serialize(config, JsonDefaults.Options), Encoding.UTF8);
        await File.WriteAllTextAsync(ignorePath, "# One glob per line\nbin/\nobj/\n", Encoding.UTF8);
        return 0;
    }

    public async Task<int> ExecutePrintConfigAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = _configLoader.LoadConfig(args);
        _ = stderr;
        await _outputWriter.WriteJsonOutputAsync(stdout, config);
        return 0;
    }

    public async Task<int> ExecutePrintFormatConfigAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stderr;

        // Determine file path for resolution (first path or current directory)
        var filePath = args.Paths.FirstOrDefault() ?? Directory.GetCurrentDirectory();

        var resolved = _formattingOptionsResolver.ResolveFormattingOptionsWithSources(args, filePath);

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await WriteFormatConfigJsonAsync(stdout, resolved);
        }
        else
        {
            await WriteFormatConfigTextAsync(stdout, resolved, args.ShowSources);
        }

        return 0;
    }

    private static async Task WriteFormatConfigJsonAsync(TextWriter stdout, ResolvedFormattingOptions resolved)
    {
        var output = new
        {
            options = new
            {
                compatLevel = new { value = resolved.CompatLevel.Value, source = FormatSource(resolved.CompatLevel.Source) },
                indentStyle = new { value = resolved.IndentStyle.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.IndentStyle.Source) },
                indentSize = new { value = resolved.IndentSize.Value, source = FormatSource(resolved.IndentSize.Source) },
                keywordCasing = new { value = resolved.KeywordCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.KeywordCasing.Source) },
                builtInFunctionCasing = new { value = resolved.BuiltInFunctionCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.BuiltInFunctionCasing.Source) },
                dataTypeCasing = new { value = resolved.DataTypeCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.DataTypeCasing.Source) },
                schemaCasing = new { value = resolved.SchemaCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.SchemaCasing.Source) },
                tableCasing = new { value = resolved.TableCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.TableCasing.Source) },
                columnCasing = new { value = resolved.ColumnCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.ColumnCasing.Source) },
                variableCasing = new { value = resolved.VariableCasing.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.VariableCasing.Source) },
                commaStyle = new { value = resolved.CommaStyle.Value.ToString().ToLowerInvariant(), source = FormatSource(resolved.CommaStyle.Source) },
                maxLineLength = new { value = resolved.MaxLineLength.Value, source = FormatSource(resolved.MaxLineLength.Source) },
                insertFinalNewline = new { value = resolved.InsertFinalNewline.Value, source = FormatSource(resolved.InsertFinalNewline.Source) },
                trimTrailingWhitespace = new { value = resolved.TrimTrailingWhitespace.Value, source = FormatSource(resolved.TrimTrailingWhitespace.Source) },
                normalizeInlineSpacing = new { value = resolved.NormalizeInlineSpacing.Value, source = FormatSource(resolved.NormalizeInlineSpacing.Source) }
            },
            sourcePaths = new
            {
                config = resolved.ConfigPath,
                editorconfig = resolved.EditorConfigPath
            },
            resolvedForPath = resolved.ResolvedForPath
        };

        var json = System.Text.Json.JsonSerializer.Serialize(output, JsonDefaults.Options);
        await stdout.WriteLineAsync(json);
    }

    private static string FormatSource(FormattingOptionSource source) => source switch
    {
        FormattingOptionSource.Default => "default",
        FormattingOptionSource.Config => "config",
        FormattingOptionSource.EditorConfig => "editorconfig",
        FormattingOptionSource.CliArg => "cli",
        _ => "unknown"
    };

    private static async Task WriteFormatConfigTextAsync(TextWriter stdout, ResolvedFormattingOptions resolved, bool showSources)
    {
        await stdout.WriteLineAsync("Effective Formatting Options:");

        await WriteOptionLineAsync(stdout, "compatLevel", resolved.CompatLevel.Value.ToString(), resolved.CompatLevel.Source, showSources);
        await WriteOptionLineAsync(stdout, "indentStyle", resolved.IndentStyle.Value.ToString().ToLowerInvariant(), resolved.IndentStyle.Source, showSources);
        await WriteOptionLineAsync(stdout, "indentSize", resolved.IndentSize.Value.ToString(), resolved.IndentSize.Source, showSources);
        await WriteOptionLineAsync(stdout, "keywordCasing", resolved.KeywordCasing.Value.ToString().ToLowerInvariant(), resolved.KeywordCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "builtInFunctionCasing", resolved.BuiltInFunctionCasing.Value.ToString().ToLowerInvariant(), resolved.BuiltInFunctionCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "dataTypeCasing", resolved.DataTypeCasing.Value.ToString().ToLowerInvariant(), resolved.DataTypeCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "schemaCasing", resolved.SchemaCasing.Value.ToString().ToLowerInvariant(), resolved.SchemaCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "tableCasing", resolved.TableCasing.Value.ToString().ToLowerInvariant(), resolved.TableCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "columnCasing", resolved.ColumnCasing.Value.ToString().ToLowerInvariant(), resolved.ColumnCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "variableCasing", resolved.VariableCasing.Value.ToString().ToLowerInvariant(), resolved.VariableCasing.Source, showSources);
        await WriteOptionLineAsync(stdout, "commaStyle", resolved.CommaStyle.Value.ToString().ToLowerInvariant(), resolved.CommaStyle.Source, showSources);
        await WriteOptionLineAsync(stdout, "maxLineLength", resolved.MaxLineLength.Value.ToString(), resolved.MaxLineLength.Source, showSources);
        await WriteOptionLineAsync(stdout, "insertFinalNewline", resolved.InsertFinalNewline.Value.ToString().ToLowerInvariant(), resolved.InsertFinalNewline.Source, showSources);
        await WriteOptionLineAsync(stdout, "trimTrailingWhitespace", resolved.TrimTrailingWhitespace.Value.ToString().ToLowerInvariant(), resolved.TrimTrailingWhitespace.Source, showSources);
        await WriteOptionLineAsync(stdout, "normalizeInlineSpacing", resolved.NormalizeInlineSpacing.Value.ToString().ToLowerInvariant(), resolved.NormalizeInlineSpacing.Source, showSources);

        if (showSources)
        {
            await stdout.WriteLineAsync();
            await stdout.WriteLineAsync("Sources:");
            if (resolved.ConfigPath is not null)
                await stdout.WriteLineAsync($"  config:       {resolved.ConfigPath}");
            if (resolved.EditorConfigPath is not null)
                await stdout.WriteLineAsync($"  editorconfig: {resolved.EditorConfigPath}");
            if (resolved.ResolvedForPath is not null)
                await stdout.WriteLineAsync($"  resolvedFor:  {resolved.ResolvedForPath}");
        }
    }

    private static async Task WriteOptionLineAsync(TextWriter stdout, string name, string value, FormattingOptionSource source, bool showSources)
    {
        var paddedName = name.PadRight(24);
        var paddedValue = value.PadRight(12);

        if (showSources)
        {
            var sourceText = source switch
            {
                FormattingOptionSource.Default => "(default)",
                FormattingOptionSource.Config => "(tsqlrefine.json)",
                FormattingOptionSource.EditorConfig => "(.editorconfig)",
                FormattingOptionSource.CliArg => "(CLI arg)",
                _ => ""
            };
            await stdout.WriteLineAsync($"  {paddedName}{paddedValue}{sourceText}");
        }
        else
        {
            await stdout.WriteLineAsync($"  {paddedName}{value}");
        }
    }

    public async Task<int> ExecuteListRulesAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = _configLoader.LoadConfig(args);
        var rules = _configLoader.LoadRules(args, config, stderr).OrderBy(r => r.Metadata.RuleId).ToArray();

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            var ruleInfos = rules.Select(r => new
            {
                id = r.Metadata.RuleId,
                description = r.Metadata.Description,
                category = r.Metadata.Category,
                defaultSeverity = r.Metadata.DefaultSeverity.ToString().ToLowerInvariant(),
                fixable = r.Metadata.Fixable,
                minCompatLevel = r.Metadata.MinCompatLevel,
                maxCompatLevel = r.Metadata.MaxCompatLevel
            });
            await _outputWriter.WriteJsonOutputAsync(stdout, ruleInfos);
        }
        else
        {
            foreach (var rule in rules)
            {
                await stdout.WriteLineAsync($"{rule.Metadata.RuleId}\t{rule.Metadata.Category}\t{rule.Metadata.DefaultSeverity}\tfixable={rule.Metadata.Fixable}");
            }
        }

        return 0;
    }

    public async Task<int> ExecuteListPluginsAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stderr;
        var config = _configLoader.LoadConfig(args);
        var plugins = (config.Plugins ?? Array.Empty<PluginConfig>())
            .Select(p => new PluginDescriptor(p.Path, p.Enabled))
            .ToArray();

        var (loaded, _) = PluginLoader.LoadWithSummary(plugins);
        await _pluginDiagnostics.WritePluginSummaryAsync(loaded, args.Verbose, stdout);

        return 0;
    }

    public async Task<int> ExecuteLintAsync(string command, CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var (read, errorCode) = await LoadInputsAsync(args, stdin, stderr);
        if (read is null)
        {
            return errorCode!.Value;
        }

        var config = _configLoader.LoadConfig(args);
        var ruleset = _configLoader.LoadRuleset(args, config);
        var rules = _configLoader.LoadRules(args, config, stderr);

        var engine = new TsqlRefineEngine(rules);
        var options = CreateEngineOptions(args, config, ruleset);
        var result = engine.Run(command, read.Inputs, options);

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await _outputWriter.WriteJsonOutputAsync(stdout, result);
        }
        else
        {
            foreach (var file in result.Files)
            {
                foreach (var d in file.Diagnostics)
                {
                    var start = d.Range.Start;
                    await stdout.WriteLineAsync($"{file.FilePath}:{start.Line + 1}:{start.Character + 1}: {d.Severity}: {d.Message} ({d.Data?.RuleId ?? d.Code})");
                }
            }
        }

        var hasParseErrors = result.Files.Any(f =>
            f.Diagnostics.Any(d => d.Code == TsqlRefineEngine.ParseErrorCode));
        if (hasParseErrors)
        {
            return ExitCodes.AnalysisError;
        }

        var hasIssues = result.Files.Any(f => f.Diagnostics.Count > 0);
        return hasIssues ? ExitCodes.Violations : 0;
    }

    public async Task<int> ExecuteFormatAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var (read, errorCode) = await LoadInputsAsync(args, stdin, stderr);
        if (read is null)
        {
            return errorCode!.Value;
        }

        var optionError = await _outputWriter.ValidateFormatFixOptionsAsync(args, read.Inputs.Count, outputJson: false, stderr);
        if (optionError.HasValue)
            return optionError.Value;

        foreach (var input in read.Inputs)
        {
            var options = _formattingOptionsResolver.ResolveFormattingOptions(args, input);
            var formatted = SqlFormatter.Format(input.Text, options);

            if (args.Diff)
            {
                var diff = _outputWriter.GenerateUnifiedDiff(input.FilePath, input.Text, formatted);
                if (!string.IsNullOrEmpty(diff))
                    await stdout.WriteAsync(diff);
            }
            else if (args.Write && input.FilePath != "<stdin>")
            {
                var encoding = read.WriteEncodings.TryGetValue(input.FilePath, out var resolved)
                    ? resolved
                    : Encoding.UTF8;
                await File.WriteAllTextAsync(input.FilePath, formatted, encoding);
            }
            else
            {
                await stdout.WriteAsync(formatted);
            }
        }

        return 0;
    }

    public async Task<int> ExecuteFixAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var (read, errorCode) = await LoadInputsAsync(args, stdin, stderr);
        if (read is null)
        {
            return errorCode!.Value;
        }

        var outputJson = string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase);
        var optionError = await _outputWriter.ValidateFormatFixOptionsAsync(args, read.Inputs.Count, outputJson, stderr);
        if (optionError.HasValue)
            return optionError.Value;

        var config = _configLoader.LoadConfig(args);
        var ruleset = _configLoader.LoadRuleset(args, config);
        var rules = _configLoader.LoadRules(args, config, stderr);

        var engine = new TsqlRefineEngine(rules);
        var options = CreateEngineOptions(args, config, ruleset);
        var result = engine.Fix(read.Inputs, options);

        if (outputJson)
        {
            var lintResult = new LintResult(
                Tool: result.Tool,
                Version: result.Version,
                Command: result.Command,
                Files: result.Files.Select(f => new FileResult(f.FilePath, f.Diagnostics)).ToArray()
            );
            await _outputWriter.WriteJsonOutputAsync(stdout, lintResult);
        }
        else
        {
            foreach (var file in result.Files)
            {
                if (args.Diff)
                {
                    var diff = _outputWriter.GenerateUnifiedDiff(file.FilePath, file.OriginalText, file.FixedText);
                    if (!string.IsNullOrEmpty(diff))
                        await stdout.WriteAsync(diff);
                }
                else if (args.Write && file.FilePath != "<stdin>")
                {
                    if (!string.Equals(file.OriginalText, file.FixedText, StringComparison.Ordinal))
                    {
                        var encoding = read.WriteEncodings.TryGetValue(file.FilePath, out var resolved)
                            ? resolved
                            : Encoding.UTF8;
                        await File.WriteAllTextAsync(file.FilePath, file.FixedText, encoding);
                    }
                }
                else
                {
                    await stdout.WriteAsync(file.FixedText);
                }
            }
        }

        var hasParseErrors = result.Files.Any(f =>
            f.Diagnostics.Any(d => d.Code == TsqlRefineEngine.ParseErrorCode));
        if (hasParseErrors)
        {
            return ExitCodes.AnalysisError;
        }

        var hasIssues = result.Files.Any(f => f.Diagnostics.Count > 0);
        return hasIssues ? ExitCodes.Violations : 0;
    }

    private EngineOptions CreateEngineOptions(CliArgs args, TsqlRefineConfig config, Ruleset? ruleset)
    {
        var minimumSeverity = args.MinimumSeverity ?? DiagnosticSeverity.Warning;
        return new EngineOptions(
            CompatLevel: args.CompatLevel ?? config.CompatLevel,
            MinimumSeverity: minimumSeverity,
            Ruleset: ruleset
        );
    }

    private async Task<(InputReader.ReadInputsResult? Read, int? ErrorCode)> LoadInputsAsync(
        CliArgs args,
        TextReader stdin,
        TextWriter stderr)
    {
        var ignorePatterns = _configLoader.LoadIgnorePatterns(args.IgnoreListPath);
        var read = await _inputReader.ReadInputsAsync(args, stdin, ignorePatterns, stderr);

        if (read.Inputs.Count == 0)
        {
            var errorCode = await _outputWriter.WriteErrorAsync(stderr, "No input.");
            return (null, errorCode);
        }

        return (read, null);
    }
}
