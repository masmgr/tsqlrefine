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
            await FormatConfigWriter.WriteJsonAsync(stdout, resolved);
        }
        else
        {
            await FormatConfigWriter.WriteTextAsync(stdout, resolved, args.ShowSources);
        }

        return 0;
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

            if (args.Write && input.FilePath != "<stdin>")
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
        var rules = _configLoader.LoadRules(args, config, stderr);

        // --rule オプションのバリデーション（存在確認 + Fixable 確認）
        _configLoader.ValidateRuleIdForFix(args, rules);

        var ruleset = _configLoader.LoadRuleset(args, config);

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
                if (args.Write && file.FilePath != "<stdin>")
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
