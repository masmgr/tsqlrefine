using System.Diagnostics;
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
    private readonly InputReader _inputReader;

    public CommandExecutor(InputReader inputReader)
    {
        _inputReader = inputReader;
    }

    public static async Task<int> ExecuteInitAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
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
        var rules = ConfigLoader.LoadRules(args, config, stderr).OrderBy(r => r.Metadata.RuleId).ToArray();

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
            await OutputWriter.WriteJsonOutputAsync(stdout, ruleInfos);
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

    public static async Task<int> ExecuteListPluginsAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stderr;
        var config = ConfigLoader.LoadConfig(args);
        var plugins = (config.Plugins ?? Array.Empty<PluginConfig>())
            .Select(p => new PluginDescriptor(p.Path, p.Enabled))
            .ToArray();

        var (loaded, _) = PluginLoader.LoadWithSummary(plugins);
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
        var ruleset = ConfigLoader.LoadRuleset(args, config);
        var rules = ConfigLoader.LoadRules(args, config, stderr);

        var engine = new TsqlRefineEngine(rules);
        var options = CreateEngineOptions(args, config, ruleset);
        var result = engine.Run(command, read.Inputs, options);

        stopwatch.Stop();

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await OutputWriter.WriteJsonOutputAsync(stdout, result);
        }
        else
        {
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
                }
            }
        }

        if (args.Verbose)
        {
            var fileCount = result.Files.Count;
            var errorCount = result.Files.Sum(f => f.Diagnostics.Count);
            var elapsed = stopwatch.Elapsed;
            var elapsedText = elapsed.TotalSeconds >= 1
                ? $"{elapsed.TotalSeconds:F2}s"
                : $"{elapsed.TotalMilliseconds:F0}ms";
            await stderr.WriteLineAsync();
            await stderr.WriteLineAsync($"Files: {fileCount}, Errors: {errorCount}, Time: {elapsedText}");
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

        foreach (var input in read.Inputs)
        {
            var options = FormattingOptionsResolver.ResolveFormattingOptions(args, input);
            var formatted = SqlFormatter.Format(input.Text, options);

            if (input.FilePath != "<stdin>")
            {
                var hasChanges = !string.Equals(input.Text, formatted, StringComparison.Ordinal);
                if (hasChanges)
                {
                    var encoding = read.WriteEncodings.TryGetValue(input.FilePath, out var resolved)
                        ? resolved
                        : Encoding.UTF8;
                    await File.WriteAllTextAsync(input.FilePath, formatted, encoding);
                    await stderr.WriteLineAsync($"Formatted: {input.FilePath}");
                }
                else
                {
                    await stderr.WriteLineAsync($"Unchanged: {input.FilePath}");
                }
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

        var config = ConfigLoader.LoadConfig(args);
        var rules = ConfigLoader.LoadRules(args, config, stderr);

        // --rule オプションのバリデーション（存在確認 + Fixable 確認）
        ConfigLoader.ValidateRuleIdForFix(args, rules);

        var ruleset = ConfigLoader.LoadRuleset(args, config);

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
                if (file.FilePath != "<stdin>")
                {
                    var hasChanges = !string.Equals(file.OriginalText, file.FixedText, StringComparison.Ordinal);
                    if (hasChanges)
                    {
                        var encoding = read.WriteEncodings.TryGetValue(file.FilePath, out var resolved)
                            ? resolved
                            : Encoding.UTF8;
                        await File.WriteAllTextAsync(file.FilePath, file.FixedText, encoding);
                        var fixCount = file.AppliedFixes.Count;
                        var fixLabel = fixCount == 1 ? "fix" : "fixes";
                        await stderr.WriteLineAsync($"Fixed: {file.FilePath} ({fixCount} {fixLabel} applied)");
                    }
                    else
                    {
                        await stderr.WriteLineAsync($"Unchanged: {file.FilePath}");
                    }
                }
                else
                {
                    await stdout.WriteAsync(file.FixedText);
                }
            }
        }

        // fix コマンドは修正の適用に問題がなければ成功（パースエラーのみ失敗扱い）
        var hasParseErrors = result.Files.Any(f =>
            f.Diagnostics.Any(d => d.Code == TsqlRefineEngine.ParseErrorCode));
        return hasParseErrors ? ExitCodes.AnalysisError : 0;
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
}
