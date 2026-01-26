using System.Reflection;
using System.Text;
using System.Text.Json;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using TsqlRefine.Core;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.Formatting;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Cli;

public static class CliApp
{
    private sealed record ReadInputsResult(List<SqlInput> Inputs, IReadOnlyDictionary<string, Encoding> WriteEncodings);

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
        EnsureEncodingProvidersRegistered();
        var parsed = CliParser.Parse(args);
        return await RunParsedAsync(parsed, stdin, stdout, stderr);
    }

    public static async Task<int> RunAsync(string[] args, Stream stdin, TextWriter stdout, TextWriter stderr)
    {
        EnsureEncodingProvidersRegistered();
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
        EnsureEncodingProvidersRegistered();

        if (parsed.ShowVersion)
        {
            await stdout.WriteLineAsync($"tsqlrefine {GetVersionString()}");
            return 0;
        }

        if (parsed.ShowHelp)
        {
            await stdout.WriteLineAsync(HelpText);
            return 0;
        }

        try
        {
            return parsed.Command switch
            {
                "init" => await RunInitAsync(parsed, stdout, stderr),
                "print-config" => await RunPrintConfigAsync(parsed, stdout, stderr),
                "list-rules" => await RunListRulesAsync(parsed, stdout, stderr),
                "list-plugins" => await RunListPluginsAsync(parsed, stdout, stderr),
                "format" => await RunFormatAsync(parsed, stdin, stdout, stderr),
                "fix" => await RunFixAsync(parsed, stdin, stdout, stderr),
                "lint" => await RunLintAsync("lint", parsed, stdin, stdout, stderr),
                "check" => await RunLintAsync("check", parsed, stdin, stdout, stderr),
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
        _ = command;
        await stderr.WriteLineAsync("Unknown command.");
        return ExitCodes.Fatal;
    }

    private static async Task<int> RunInitAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
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

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonDefaults.Options), Encoding.UTF8);
        await File.WriteAllTextAsync(ignorePath, "# One glob per line\nbin/\nobj/\n", Encoding.UTF8);
        return 0;
    }

    private static async Task<int> RunPrintConfigAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = LoadConfig(args);
        _ = stderr;
        await stdout.WriteLineAsync(JsonSerializer.Serialize(config, JsonDefaults.Options));
        return 0;
    }

    private static Task<int> RunListRulesAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        var config = LoadConfig(args);
        var rules = LoadRules(args, config, stderr).OrderBy(r => r.Metadata.RuleId).ToArray();
        foreach (var rule in rules)
        {
            stdout.WriteLine($"{rule.Metadata.RuleId}\t{rule.Metadata.Category}\t{rule.Metadata.DefaultSeverity}\tfixable={rule.Metadata.Fixable}");
        }

        return Task.FromResult(0);
    }

    private static Task<int> RunListPluginsAsync(CliArgs args, TextWriter stdout, TextWriter stderr)
    {
        _ = stderr;
        var config = LoadConfig(args);
        var plugins = (config.Plugins ?? Array.Empty<PluginConfig>())
            .Select(p => new PluginDescriptor(p.Path, p.Enabled))
            .ToArray();

        var loaded = PluginLoader.Load(plugins);

        foreach (var p in loaded)
        {
            var statusIcon = p.Diagnostic.Status switch
            {
                PluginLoadStatus.Success => "✓",
                PluginLoadStatus.Disabled => "○",
                PluginLoadStatus.FileNotFound => "✗",
                PluginLoadStatus.LoadError => "✗",
                PluginLoadStatus.VersionMismatch => "⚠",
                PluginLoadStatus.NoProviders => "⚠",
                _ => "?"
            };

            stdout.WriteLine($"{statusIcon} {p.Path}");
            stdout.WriteLine($"  Status: {p.Diagnostic.Status}");

            if (!p.Enabled)
            {
                stdout.WriteLine($"  Message: {p.Diagnostic.Message}");
            }
            else if (p.Diagnostic.Status == PluginLoadStatus.Success)
            {
                stdout.WriteLine($"  Providers: {p.Providers.Count}");
                var ruleCount = p.Providers.SelectMany(prov => prov.GetRules()).Count();
                stdout.WriteLine($"  Rules: {ruleCount}");
            }
            else if (p.Diagnostic.Status == PluginLoadStatus.VersionMismatch)
            {
                stdout.WriteLine($"  Message: {p.Diagnostic.Message}");
                stdout.WriteLine($"  Expected API Version: {p.Diagnostic.ExpectedApiVersion}");
                stdout.WriteLine($"  Actual API Version: {p.Diagnostic.ActualApiVersion}");
            }
            else if (p.Diagnostic.Status == PluginLoadStatus.LoadError)
            {
                stdout.WriteLine($"  Error: {p.Diagnostic.ExceptionType}: {p.Diagnostic.Message}");
                if (!string.IsNullOrWhiteSpace(p.Diagnostic.StackTrace))
                {
                    stdout.WriteLine($"  Stack Trace:");
                    foreach (var line in p.Diagnostic.StackTrace.Split('\n'))
                    {
                        stdout.WriteLine($"    {line.TrimEnd()}");
                    }
                }
            }
            else
            {
                stdout.WriteLine($"  Message: {p.Diagnostic.Message}");
            }

            stdout.WriteLine();
        }

        return Task.FromResult(0);
    }

    private static async Task<int> RunLintAsync(string command, CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var read = await ReadInputsAsync(args, stdin, stderr);
        if (read.Inputs.Count == 0)
        {
            return await WriteErrorAsync(stderr, "No input.");
        }

        var config = LoadConfig(args);
        var ruleset = LoadRuleset(args, config);
        var rules = LoadRules(args, config, stderr);

        var engine = new TsqlRefineEngine(rules);
        var options = CreateEngineOptions(args, config, ruleset);
        var result = engine.Run(command, read.Inputs, options);

        if (string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase))
        {
            await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonDefaults.Options));
        }
        else
        {
            foreach (var file in result.Files)
            {
                foreach (var d in file.Diagnostics)
                {
                    await stdout.WriteLineAsync($"{file.FilePath}: {d.Severity}: {d.Message} ({d.Data?.RuleId ?? d.Code})");
                }
            }
        }

        var hasIssues = result.Files.Any(f => f.Diagnostics.Count > 0);
        return hasIssues ? ExitCodes.Violations : 0;
    }

    private static async Task<int> RunFormatAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var read = await ReadInputsAsync(args, stdin, stderr);
        if (read.Inputs.Count == 0)
        {
            return await WriteErrorAsync(stderr, "No input.");
        }

        var optionError = await ValidateFormatFixOptionsAsync(args, read.Inputs.Count, outputJson: false, stderr);
        if (optionError.HasValue)
            return optionError.Value;

        foreach (var input in read.Inputs)
        {
            var options = ResolveFormattingOptions(args, input);
            var formatted = SqlFormatter.Format(input.Text, options);

            if (args.Diff)
            {
                var diff = GenerateUnifiedDiff(input.FilePath, input.Text, formatted);
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

    private static FormattingOptions ResolveFormattingOptions(CliArgs args, SqlInput input)
    {
        var options = new FormattingOptions();
        var editorConfig = TryReadEditorConfigOptions(input.FilePath);
        if (editorConfig is not null)
        {
            options = new FormattingOptions(
                IndentStyle: editorConfig.IndentStyle ?? options.IndentStyle,
                IndentSize: editorConfig.IndentSize ?? options.IndentSize
            );
        }

        return new FormattingOptions(
            IndentStyle: args.IndentStyle ?? options.IndentStyle,
            IndentSize: args.IndentSize ?? options.IndentSize
        );
    }

    private static EditorConfigFormattingOptions? TryReadEditorConfigOptions(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.Equals(filePath, "<stdin>", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var parser = new EditorConfig.Core.EditorConfigParser();
            var config = parser.Parse(Path.GetFullPath(filePath));
            if (config?.Properties is null)
            {
                return null;
            }

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in config.Properties)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                properties[entry.Key] = entry.Value ?? string.Empty;
            }

            var indentStyle = ParseEditorConfigIndentStyle(properties);
            var indentSize = ParseEditorConfigIndentSize(properties);
            if (indentStyle is null && indentSize is null)
            {
                return null;
            }

            return new EditorConfigFormattingOptions(indentStyle, indentSize);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse .editorconfig: {ex.Message}");
        }
    }

    private static IndentStyle? ParseEditorConfigIndentStyle(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_style", out var styleValue))
        {
            return null;
        }

        return styleValue.Trim().ToLowerInvariant() switch
        {
            "tab" => IndentStyle.Tabs,
            "space" => IndentStyle.Spaces,
            _ => null
        };
    }

    private static int? ParseEditorConfigIndentSize(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("indent_size", out var sizeValue) || string.IsNullOrWhiteSpace(sizeValue))
        {
            return null;
        }

        sizeValue = sizeValue.Trim();
        if (string.Equals(sizeValue, "tab", StringComparison.OrdinalIgnoreCase))
        {
            if (properties.TryGetValue("tab_width", out var tabWidthValue) &&
                int.TryParse(tabWidthValue.Trim(), out var tabWidth) &&
                tabWidth > 0)
            {
                return tabWidth;
            }

            return null;
        }

        return int.TryParse(sizeValue, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static async Task<int> RunFixAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var read = await ReadInputsAsync(args, stdin, stderr);
        if (read.Inputs.Count == 0)
        {
            return await WriteErrorAsync(stderr, "No input.");
        }

        var outputJson = string.Equals(args.Output, "json", StringComparison.OrdinalIgnoreCase);
        var optionError = await ValidateFormatFixOptionsAsync(args, read.Inputs.Count, outputJson, stderr);
        if (optionError.HasValue)
            return optionError.Value;

        var config = LoadConfig(args);
        var ruleset = LoadRuleset(args, config);
        var rules = LoadRules(args, config, stderr);

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
            await stdout.WriteLineAsync(JsonSerializer.Serialize(lintResult, JsonDefaults.Options));
        }
        else
        {
            foreach (var file in result.Files)
            {
                if (args.Diff)
                {
                    var diff = GenerateUnifiedDiff(file.FilePath, file.OriginalText, file.FixedText);
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

        var hasIssues = result.Files.Any(f => f.Diagnostics.Count > 0);
        return hasIssues ? ExitCodes.Violations : 0;
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

    private static async Task<int?> ValidateFormatFixOptionsAsync(
        CliArgs args,
        int inputCount,
        bool outputJson,
        TextWriter stderr)
    {
        if (args.Diff && args.Write)
        {
            return await WriteErrorAsync(stderr, "--diff and --write are mutually exclusive.");
        }

        if (outputJson && args.Diff)
        {
            return await WriteErrorAsync(stderr, "--diff cannot be used with --output json.");
        }

        if (!outputJson && !args.Write && !args.Diff && inputCount > 1)
        {
            return await WriteErrorAsync(stderr, "Multiple inputs require --write or --diff.");
        }

        return null;
    }

    private static TsqlRefineConfig LoadConfig(CliArgs args)
    {
        var path = args.ConfigPath;
        if (path is null)
        {
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.json");
            if (File.Exists(defaultPath))
            {
                path = defaultPath;
            }
        }

        if (path is null)
        {
            return TsqlRefineConfig.Default;
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Config file not found: {path}");
        }

        try
        {
            return TsqlRefineConfig.Load(path);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse config: {ex.Message}");
        }
    }

    private static Ruleset? LoadRuleset(CliArgs args, TsqlRefineConfig config)
    {
        var path = args.RulesetPath ?? config.Ruleset;

        if (!string.IsNullOrWhiteSpace(args.Preset))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "rulesets", $"{args.Preset}.json");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Ruleset file not found: {path}");
        }

        try
        {
            return Ruleset.Load(path);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse ruleset: {ex.Message}");
        }
    }

    private static IReadOnlyList<IRule> LoadRules(CliArgs args, TsqlRefineConfig config, TextWriter? stderr = null)
    {
        _ = args;

        var rules = new List<IRule>();
        rules.AddRange(new BuiltinRuleProvider().GetRules());

        var plugins = (config.Plugins ?? Array.Empty<PluginConfig>())
            .Select(p => new PluginDescriptor(p.Path, p.Enabled))
            .ToArray();

        var loaded = PluginLoader.Load(plugins);

        foreach (var p in loaded)
        {
            // Report plugin loading issues to stderr if available
            if (stderr is not null && p.Diagnostic.Status != PluginLoadStatus.Success && p.Diagnostic.Status != PluginLoadStatus.Disabled)
            {
                var warningPrefix = "Warning: Plugin loading issue - ";
                stderr.WriteLine($"{warningPrefix}{p.Path}");

                if (p.Diagnostic.Status == PluginLoadStatus.VersionMismatch)
                {
                    stderr.WriteLine($"  API version mismatch: plugin uses v{p.Diagnostic.ActualApiVersion}, host expects v{p.Diagnostic.ExpectedApiVersion}");
                }
                else if (p.Diagnostic.Status == PluginLoadStatus.LoadError)
                {
                    stderr.WriteLine($"  Load error: {p.Diagnostic.ExceptionType}: {p.Diagnostic.Message}");
                }
                else if (p.Diagnostic.Status == PluginLoadStatus.FileNotFound)
                {
                    stderr.WriteLine($"  File not found");
                }
                else if (p.Diagnostic.Status == PluginLoadStatus.NoProviders)
                {
                    stderr.WriteLine($"  No rule providers found in assembly");
                }

                stderr.WriteLine();
            }

            foreach (var provider in p.Providers)
            {
                rules.AddRange(provider.GetRules());
            }
        }

        return rules;
    }

    private static async Task<ReadInputsResult> ReadInputsAsync(CliArgs args, TextReader stdin, TextWriter stderr)
    {
        var inputs = new List<SqlInput>();
        var encodings = new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase);
        var paths = args.Paths.ToArray();

        if (args.Stdin || paths.Any(p => p == "-"))
        {
            var sql = await stdin.ReadToEndAsync();
            var filePath = args.StdinFilePath ?? "<stdin>";
            inputs.Add(new SqlInput(filePath, sql));
            paths = paths.Where(p => p != "-").ToArray();
        }

        var ignorePatterns = LoadIgnorePatterns(args.IgnoreListPath, stderr);
        foreach (var path in ExpandPaths(paths, ignorePatterns))
        {
            if (!File.Exists(path))
            {
                await stderr.WriteLineAsync($"File not found: {path}");
                continue;
            }

            if (args.DetectEncoding)
            {
                var decoded = await CharsetDetection.ReadFileAsync(path);
                inputs.Add(new SqlInput(path, decoded.Text));
                encodings[path] = decoded.WriteEncoding;
            }
            else
            {
                var sql = await File.ReadAllTextAsync(path, Encoding.UTF8);
                inputs.Add(new SqlInput(path, sql));
            }
        }

        return new ReadInputsResult(inputs, encodings);
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths, List<string> ignorePatterns)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude("**/*.sql");

                foreach (var pattern in ignorePatterns)
                    matcher.AddExclude(pattern);

                var result = matcher.Execute(
                    new DirectoryInfoWrapper(new DirectoryInfo(path)));

                foreach (var file in result.Files)
                    yield return Path.Combine(path, file.Path);

                continue;
            }

            // For individual files, check if they match ignore patterns
            if (!ShouldIgnoreFile(path, ignorePatterns))
                yield return path;
        }
    }

    private static bool ShouldIgnoreFile(string filePath, List<string> ignorePatterns)
    {
        if (ignorePatterns.Count == 0)
            return false;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in ignorePatterns)
            matcher.AddInclude(pattern);

        var fileName = Path.GetFileName(filePath);
        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;

        var result = matcher.Match(directoryPath, new[] { fileName });
        return result.HasMatches;
    }

    private static List<string> LoadIgnorePatterns(string? ignoreListPath, TextWriter stderr)
    {
        // Check explicit path first, then default tsqlrefine.ignore
        var path = ignoreListPath;
        if (path is null)
        {
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "tsqlrefine.ignore");
            if (File.Exists(defaultPath))
                path = defaultPath;
        }

        if (path is null)
            return new List<string>();

        if (!File.Exists(path))
            throw new ConfigException($"Ignore list file not found: {path}");

        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            return lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('#'))
                .ToList();
        }
        catch (Exception ex) when (ex is not ConfigException)
        {
            throw new ConfigException($"Failed to read ignore list: {ex.Message}");
        }
    }

    private static int _encodingProvidersRegistered;

    private static void EnsureEncodingProvidersRegistered()
    {
        if (Interlocked.Exchange(ref _encodingProvidersRegistered, 1) == 1)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static async Task<int> WriteErrorAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return ExitCodes.Fatal;
    }

    private static string GenerateUnifiedDiff(string filePath, string original, string formatted)
    {
        if (string.Equals(original, formatted, StringComparison.Ordinal))
            return string.Empty;

        var differ = new Differ();
        var builder = new InlineDiffBuilder(differ);

        var diff = builder.BuildDiffModel(original, formatted, ignoreWhitespace: false);

        var result = new StringBuilder();
        result.AppendLine($"--- {filePath}");
        result.AppendLine($"+++ {filePath}");

        // Generate line-by-line diff
        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Deleted => "-",
                ChangeType.Inserted => "+",
                ChangeType.Modified => "!",
                _ => " "
            };

            result.AppendLine($"{prefix}{line.Text}");
        }

        return result.ToString();
    }

    private const string HelpText =
        """
        tsqlrefine [global options] [paths...]
        tsqlrefine <command> [options] [paths...]

        Commands:
          lint, check, format, fix, init, print-config, list-rules, list-plugins

        Options:
          -c, --config <path>
          -g, --ignorelist <path>
              --detect-encoding            (auto-detect input file/stdin encodings)
              --stdin
              --stdin-filepath <path>
              --output <text|json>
              --severity <error|warning|info|hint>
              --preset <recommended|strict|security-only>
              --compat-level <110|120|130|140|150|160>
              --ruleset <path>
              --write                      (format/fix)
              --diff                       (format/fix)

          -h, --help
          -v, --version
        """;

    private sealed record EditorConfigFormattingOptions(
        IndentStyle? IndentStyle,
        int? IndentSize);
}
