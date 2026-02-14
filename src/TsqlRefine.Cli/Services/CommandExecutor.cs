using System.Diagnostics;
using System.Globalization;
using System.Text;
using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.Formatting;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Executes CLI commands (lint, format, fix, etc.) and returns appropriate exit codes.
/// </summary>
public sealed class CommandExecutor
{
    private const string StdinFilePath = "<stdin>";
    private readonly InputReader _inputReader;

    public CommandExecutor(InputReader inputReader)
    {
        _inputReader = inputReader;
    }

    private const string ConfigDirName = ".tsqlrefine";

    public static async Task<int> ExecuteInitAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var baseDir = args.Global
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ConfigDirName)
            : Path.Combine(Directory.GetCurrentDirectory(), ConfigDirName);

        var configPath = Path.Combine(baseDir, "tsqlrefine.json");
        var ignorePath = Path.Combine(baseDir, "tsqlrefine.ignore");

        if (!args.Force && (File.Exists(configPath) || File.Exists(ignorePath)))
        {
            await stderr.WriteLineAsync("Config files already exist. Use --force to overwrite.");
            return ExitCodes.Fatal;
        }

        Directory.CreateDirectory(baseDir);

        var preset = args.Preset ?? "recommended";
        var config = new TsqlRefineConfig(
            CompatLevel: args.CompatLevel ?? 150,
            Preset: preset,
            Plugins: Array.Empty<PluginConfig>()
        );

        // Serialize with $schema reference for IDE support
        var jsonNode = System.Text.Json.JsonSerializer.SerializeToNode(config, JsonDefaults.Options)!;
        var jsonObj = jsonNode.AsObject();
        jsonObj.Insert(0, "$schema", "../schemas/tsqlrefine.schema.json");
        var json = System.Text.Json.JsonSerializer.Serialize(jsonObj, JsonDefaults.Options);

        await File.WriteAllTextAsync(configPath, json, Encoding.UTF8);
        await File.WriteAllTextAsync(ignorePath, "# One glob per line\nbin/\nobj/\n", Encoding.UTF8);

        await stdout.WriteLineAsync($"Created {configPath}");
        await stdout.WriteLineAsync($"Created {ignorePath}");
        return 0;
    }

    public static async Task<int> ExecutePrintConfigAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = ConfigLoader.LoadConfig(args);
        _ = stderr;
        await OutputWriter.WriteJsonOutputAsync(stdout, config);
        return 0;
    }

    public static async Task<int> ExecutePrintFormatConfigAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stderr;

        // Determine file path for resolution (first path or current directory)
        var filePath = args.Paths.Count > 0 ? args.Paths[0] : Directory.GetCurrentDirectory();

        var resolved = FormattingOptionsResolver.ResolveFormattingOptionsWithSources(args, filePath);

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await FormatConfigWriter.WriteJsonAsync(stdout, resolved);
        }
        else
        {
            await FormatConfigWriter.WriteTextAsync(stdout, resolved, args.ShowSources);
        }

        return 0;
    }

    public static async Task<int> ExecuteListRulesAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = ConfigLoader.LoadConfig(args);
        var allRules = ConfigLoader.LoadRules(args, config, stderr).OrderBy(r => r.Metadata.RuleId).ToArray();

        // Apply filters
        IEnumerable<IRule> filtered = allRules;

        if (!string.IsNullOrWhiteSpace(args.Category))
        {
            filtered = filtered.Where(r =>
                string.Equals(r.Metadata.Category, args.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (args.FixableOnly)
        {
            filtered = filtered.Where(r => r.Metadata.Fixable);
        }

        var ruleset = ConfigLoader.LoadRuleset(args, config, allRules);

        if (args.EnabledOnly)
        {
            filtered = filtered.Where(r => ruleset.IsRuleEnabled(r.Metadata.RuleId));
        }

        var rules = filtered.ToArray();

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            var ruleInfos = rules.Select(r => new
            {
                id = r.Metadata.RuleId,
                description = r.Metadata.Description,
                category = r.Metadata.Category,
                defaultSeverity = r.Metadata.DefaultSeverity.ToString().ToLowerInvariant(),
                effectiveSeverity = GetEffectiveSeverity(r, ruleset),
                fixable = r.Metadata.Fixable,
                enabled = ruleset.IsRuleEnabled(r.Metadata.RuleId),
                minCompatLevel = r.Metadata.MinCompatLevel,
                maxCompatLevel = r.Metadata.MaxCompatLevel,
                documentationUri = r.Metadata.DocumentationUri?.ToString(),
            });
            await OutputWriter.WriteJsonOutputAsync(stdout, ruleInfos);
        }
        else
        {
            await WriteRulesTableAsync(stdout, rules, ruleset);
        }

        return 0;
    }

    private static async Task WriteRulesTableAsync(TextWriter stdout, IRule[] rules, Ruleset ruleset)
    {
        const int idWidth = 38;
        const int categoryWidth = 16;
        const int severityWidth = 13;
        const int fixableWidth = 7;
        const int enabledWidth = 7;

        var header = $"{"Rule ID",-idWidth} {"Category",-categoryWidth} {"Severity",-severityWidth} {"Fixable",-fixableWidth} {"Enabled",-enabledWidth}";

        await stdout.WriteLineAsync(header);
        await stdout.WriteLineAsync(new string('\u2500', header.Length));

        foreach (var rule in rules)
        {
            var m = rule.Metadata;
            var fixable = m.Fixable ? "Yes" : "No";
            var severity = GetEffectiveSeverity(rule, ruleset);
            var capitalizedSeverity = char.ToUpperInvariant(severity[0]) + severity[1..];
            var enabled = ruleset.IsRuleEnabled(m.RuleId) ? "Yes" : "No";
            var line = $"{m.RuleId,-idWidth} {m.Category,-categoryWidth} {capitalizedSeverity,-severityWidth} {fixable,-fixableWidth} {enabled,-enabledWidth}";

            await stdout.WriteLineAsync(line);
        }

        await stdout.WriteLineAsync();
        await stdout.WriteLineAsync($"Total: {rules.Length} rules");
    }

    /// <summary>
    /// Computes the effective severity for a rule, considering ruleset overrides.
    /// </summary>
    private static string GetEffectiveSeverity(IRule rule, Ruleset ruleset)
    {
        var defaultSeverity = rule.Metadata.DefaultSeverity.ToString().ToLowerInvariant();

        if (!ruleset.IsRuleEnabled(rule.Metadata.RuleId))
        {
            return "none";
        }

        var overrideSeverity = ruleset.GetSeverityOverride(rule.Metadata.RuleId);
        return overrideSeverity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Information => "information",
            DiagnosticSeverity.Hint => "hint",
            _ => defaultSeverity
        };
    }

    public static async Task<int> ExecuteListPluginsAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = ConfigLoader.LoadConfig(args);
        var pluginConfigs = config.Plugins ?? Array.Empty<PluginConfig>();

        if (pluginConfigs.Count == 0)
        {
            await stdout.WriteLineAsync("No plugins configured.");
            return 0;
        }

        if (!args.AllowPlugins)
        {
            await stderr.WriteLineAsync(
                $"{pluginConfigs.Count} plugin(s) configured but --allow-plugins not specified.");
            await stderr.WriteLineAsync(
                "Plugin loading is disabled by default for security. Use --allow-plugins to load plugins.");
            foreach (var p in pluginConfigs)
            {
                await stdout.WriteLineAsync($"  {p.Path} (not loaded, enabled={p.Enabled})");
            }

            return 0;
        }

        var configPath = ConfigLoader.GetConfigPath(args);
        var baseDirectory = configPath is not null
            ? Path.GetDirectoryName(Path.GetFullPath(configPath))!
            : Directory.GetCurrentDirectory();

        var plugins = ConfigLoader.ResolvePluginDescriptors(pluginConfigs, baseDirectory);

        var (loaded, _) = PluginLoader.LoadWithSummary(plugins, baseDirectory);
        await PluginDiagnostics.WritePluginSummaryAsync(loaded, args.Verbose, stdout);

        return 0;
    }

    public async Task<int> ExecuteLintAsync(string command, CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var stopwatch = Stopwatch.StartNew();

        var (read, errorCode) = await LoadInputsAsync(args, stdin, stderr);
        if (read is null)
        {
            return errorCode!.Value;
        }

        var config = ConfigLoader.LoadConfig(args);
        var rules = ConfigLoader.LoadRules(args, config, stderr);
        var ruleset = ConfigLoader.LoadRuleset(args, config, rules);

        var engine = new TsqlRefineEngine(rules);
        var options = CreateEngineOptions(args, config, ruleset);
        var result = engine.Run(command, read.Inputs, options);
        var diagnosticsSummary = SummarizeDiagnostics(result.Files);

        stopwatch.Stop();

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await OutputWriter.WriteJsonOutputAsync(stdout, result);
        }
        else
        {
            // Build source lookup for parse-error context display
            var sourceByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in read.Inputs)
            {
                sourceByPath[input.FilePath] = input.Text;
            }

            // Sort by file path, then by line, then by character
            var sortedFiles = result.Files.OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase);
            foreach (var file in sortedFiles)
            {
                var sortedDiagnostics = file.Diagnostics
                    .OrderBy(d => d.Range.Start.Line)
                    .ThenBy(d => d.Range.Start.Character);
                foreach (var d in sortedDiagnostics)
                {
                    var start = d.Range.Start;
                    var ruleId = d.Data?.RuleId ?? d.Code;
                    var fixableIndicator = d.Data?.Fixable == true ? ",Fixable" : "";
                    await stdout.WriteLineAsync($"{file.FilePath}:{start.Line + 1}:{start.Character + 1}: {d.Severity}: {d.Message} ({ruleId}{fixableIndicator})");

                    if (d.Code == TsqlRefineEngine.ParseErrorCode &&
                        sourceByPath.TryGetValue(file.FilePath, out var sourceText))
                    {
                        await WriteSourceContextAsync(stdout, sourceText, d.Range.Start);
                    }
                }
            }
        }

        if (!args.Quiet)
        {
            // Show summary to stderr (suppressed in quiet mode for IDE integration)
            await stderr.WriteLineAsync();
            await stderr.WriteLineAsync(FormatSummary(diagnosticsSummary));

            if (args.Verbose)
            {
                var elapsed = stopwatch.Elapsed;
                var elapsedText = elapsed.TotalSeconds >= 1
                    ? $"{elapsed.TotalSeconds:F2}s"
                    : $"{elapsed.TotalMilliseconds:F0}ms";
                await stderr.WriteLineAsync($"Time: {elapsedText}");
            }
        }

        if (diagnosticsSummary.HasParseErrors)
        {
            return ExitCodes.AnalysisError;
        }

        return diagnosticsSummary.TotalDiagnostics > 0 ? ExitCodes.Violations : 0;
    }

    public async Task<int> ExecuteFormatAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var (read, errorCode) = await LoadInputsAsync(args, stdin, stderr);
        if (read is null)
        {
            return errorCode!.Value;
        }

        foreach (var input in read.Inputs)
        {
            var options = FormattingOptionsResolver.ResolveFormattingOptions(args, input);
            var formatted = SqlFormatter.Format(input.Text, options);

            if (IsStdinInput(input.FilePath))
            {
                await stdout.WriteAsync(formatted);
                continue;
            }

            var hasChanges = await TryWriteFileIfChangedAsync(
                input.FilePath,
                input.Text,
                formatted,
                read.WriteEncodings);

            var message = hasChanges
                ? $"Formatted: {input.FilePath}"
                : $"Unchanged: {input.FilePath}";
            await WriteInfoAsync(stderr, args.Quiet, message);
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

        var config = ConfigLoader.LoadConfig(args);
        var rules = ConfigLoader.LoadRules(args, config, stderr);

        // --rule オプションのバリデーション（存在確認 + Fixable 確認）
        ConfigLoader.ValidateRuleIdForFix(args, rules);

        var ruleset = ConfigLoader.LoadRuleset(args, config, rules);

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
            await OutputWriter.WriteJsonOutputAsync(stdout, lintResult);
        }
        else
        {
            foreach (var file in result.Files)
            {
                if (IsStdinInput(file.FilePath))
                {
                    await stdout.WriteAsync(file.FixedText);
                    continue;
                }

                var hasChanges = await TryWriteFileIfChangedAsync(
                    file.FilePath,
                    file.OriginalText,
                    file.FixedText,
                    read.WriteEncodings);

                if (!hasChanges)
                {
                    await WriteInfoAsync(stderr, args.Quiet, $"Unchanged: {file.FilePath}");
                    continue;
                }

                var fixCount = file.AppliedFixes.Count;
                var fixLabel = fixCount == 1 ? "fix" : "fixes";
                await WriteInfoAsync(stderr, args.Quiet, $"Fixed: {file.FilePath} ({fixCount} {fixLabel} applied)");
            }
        }

        // fix コマンドは修正の適用に問題がなければ成功（パースエラーのみ失敗扱い）
        return HasParseErrors(result.Files) ? ExitCodes.AnalysisError : 0;
    }

    private static EngineOptions CreateEngineOptions(CliArgs args, TsqlRefineConfig config, Ruleset? ruleset)
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
        var ignorePatterns = ConfigLoader.LoadIgnorePatterns(args.IgnoreListPath);
        var read = await _inputReader.ReadInputsAsync(args, stdin, ignorePatterns, stderr);

        if (read.Inputs.Count == 0)
        {
            var errorCode = await OutputWriter.WriteErrorAsync(stderr, "No input.");
            return (null, errorCode);
        }

        return (read, null);
    }

    private static LintDiagnosticsSummary SummarizeDiagnostics(IReadOnlyList<FileResult> files)
    {
        var totalDiagnostics = 0;
        var errors = 0;
        var warnings = 0;
        var fixable = 0;
        var hasParseErrors = false;

        foreach (var file in files)
        {
            totalDiagnostics += file.Diagnostics.Count;

            foreach (var diagnostic in file.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    errors++;
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    warnings++;

                if (diagnostic.Data?.Fixable == true)
                    fixable++;

                if (!hasParseErrors && diagnostic.Code == TsqlRefineEngine.ParseErrorCode)
                    hasParseErrors = true;
            }
        }

        return new LintDiagnosticsSummary(totalDiagnostics, errors, warnings, fixable, files.Count, hasParseErrors);
    }

    private static string FormatSummary(LintDiagnosticsSummary summary)
    {
        var fileLabel = summary.FileCount == 1 ? "file" : "files";

        if (summary.TotalDiagnostics == 0)
        {
            return $"No problems found in {summary.FileCount} {fileLabel}.";
        }

        var parts = new List<string>(3);
        if (summary.Errors > 0)
        {
            var label = summary.Errors == 1 ? "error" : "errors";
            parts.Add($"{summary.Errors} {label}");
        }
        if (summary.Warnings > 0)
        {
            var label = summary.Warnings == 1 ? "warning" : "warnings";
            parts.Add($"{summary.Warnings} {label}");
        }
        var otherCount = summary.TotalDiagnostics - summary.Errors - summary.Warnings;
        if (otherCount > 0)
        {
            parts.Add($"{otherCount} info/hint");
        }

        var problemLabel = summary.TotalDiagnostics == 1 ? "problem" : "problems";
        var detail = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
        var result = $"{summary.TotalDiagnostics} {problemLabel}{detail} in {summary.FileCount} {fileLabel}.";

        if (summary.Fixable > 0)
        {
            result += $" {summary.Fixable} auto-fixable.";
        }

        return result;
    }

    private static bool HasParseErrors(IReadOnlyList<FixedFileResult> files)
    {
        foreach (var file in files)
        {
            foreach (var diagnostic in file.Diagnostics)
            {
                if (diagnostic.Code == TsqlRefineEngine.ParseErrorCode)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task WriteSourceContextAsync(TextWriter writer, string sourceText, Position position)
    {
        var lines = sourceText.Split('\n');
        var lineIndex = position.Line;
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return;
        }

        var sourceLine = lines[lineIndex].TrimEnd('\r');
        var lineNumber = lineIndex + 1;
        var gutterWidth = Math.Max(lineNumber.ToString(CultureInfo.InvariantCulture).Length, 1);
        var gutter = lineNumber.ToString(CultureInfo.InvariantCulture).PadLeft(gutterWidth);
        var emptyGutter = new string(' ', gutterWidth);

        await writer.WriteLineAsync($"  {gutter} | {sourceLine}");

        var caretCol = Math.Min(position.Character, sourceLine.Length);
        var padding = new string(' ', caretCol);
        await writer.WriteLineAsync($"  {emptyGutter} | {padding}^");
    }

    private static bool IsStdinInput(string filePath) =>
        string.Equals(filePath, StdinFilePath, StringComparison.Ordinal);

    private static async Task<bool> TryWriteFileIfChangedAsync(
        string filePath,
        string originalText,
        string updatedText,
        IReadOnlyDictionary<string, Encoding> writeEncodings)
    {
        if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
        {
            return false;
        }

        var encoding = writeEncodings.TryGetValue(filePath, out var resolved)
            ? resolved
            : Encoding.UTF8;
        await File.WriteAllTextAsync(filePath, updatedText, encoding);
        return true;
    }

    private static Task WriteInfoAsync(TextWriter stderr, bool quiet, string message) =>
        quiet ? Task.CompletedTask : stderr.WriteLineAsync(message);

    private readonly record struct LintDiagnosticsSummary(
        int TotalDiagnostics,
        int Errors,
        int Warnings,
        int Fixable,
        int FileCount,
        bool HasParseErrors);
}
