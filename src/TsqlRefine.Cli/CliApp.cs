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
using TsqlRefine.Formatting;
using TsqlRefine.PluginHost;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

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
        var parsed = CliParser.Parse(args);

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

    private static Task<int> UnknownCommandAsync(string command, TextWriter stderr)
    {
        _ = command;
        return stderr.WriteLineAsync("Unknown command.").ContinueWith(_ => ExitCodes.Fatal);
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
        _ = stderr;
        var config = LoadConfig(args);
        var rules = LoadRules(args, config).OrderBy(r => r.Metadata.RuleId).ToArray();
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
            stdout.WriteLine($"{p.Path}\tenabled={p.Enabled}\tproviders={p.Providers.Count}\terror={p.Error ?? "<none>"}");
        }

        return Task.FromResult(0);
    }

    private static async Task<int> RunLintAsync(string command, CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var inputs = await ReadInputsAsync(args, stdin, stderr);
        if (inputs.Count == 0)
        {
            await stderr.WriteLineAsync("No input.");
            return ExitCodes.Fatal;
        }

        var config = LoadConfig(args);
        var ruleset = LoadRuleset(args, config);
        var rules = LoadRules(args, config);

        var engine = new TsqlRefineEngine(rules);
        var minimumSeverity = args.MinimumSeverity ?? DiagnosticSeverity.Warning;

        var result = engine.Run(command, inputs, new EngineOptions(
            CompatLevel: args.CompatLevel ?? config.CompatLevel,
            MinimumSeverity: minimumSeverity,
            Ruleset: ruleset
        ));

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
        var inputs = await ReadInputsAsync(args, stdin, stderr);
        if (inputs.Count == 0)
        {
            await stderr.WriteLineAsync("No input.");
            return ExitCodes.Fatal;
        }

        if (args.Diff && args.Write)
        {
            await stderr.WriteLineAsync("--diff and --write are mutually exclusive.");
            return ExitCodes.Fatal;
        }

        if (!args.Write && !args.Diff && inputs.Count > 1)
        {
            await stderr.WriteLineAsync("Multiple inputs require --write or --diff.");
            return ExitCodes.Fatal;
        }

        var options = new FormattingOptions(
            IndentStyle: args.IndentStyle ?? IndentStyle.Spaces,
            IndentSize: args.IndentSize ?? 4
        );

        foreach (var input in inputs)
        {
            var formatted = SqlFormatter.Format(input.Text, options);

            if (args.Diff)
            {
                var diff = GenerateUnifiedDiff(input.FilePath, input.Text, formatted);
                if (!string.IsNullOrEmpty(diff))
                    await stdout.WriteAsync(diff);
            }
            else if (args.Write && input.FilePath != "<stdin>")
            {
                await File.WriteAllTextAsync(input.FilePath, formatted, Encoding.UTF8);
            }
            else
            {
                await stdout.WriteAsync(formatted);
            }
        }

        return 0;
    }

    private static Task<int> RunFixAsync(CliArgs args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        _ = stdout;
        _ = stdin;
        _ = args;
        return stderr.WriteLineAsync("fix is not implemented yet.").ContinueWith(_ => ExitCodes.Fatal);
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

    private static IReadOnlyList<IRule> LoadRules(CliArgs args, TsqlRefineConfig config)
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
            foreach (var provider in p.Providers)
            {
                rules.AddRange(provider.GetRules());
            }
        }

        return rules;
    }

    private static async Task<List<SqlInput>> ReadInputsAsync(CliArgs args, TextReader stdin, TextWriter stderr)
    {
        var inputs = new List<SqlInput>();
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

            var sql = await File.ReadAllTextAsync(path, Encoding.UTF8);
            inputs.Add(new SqlInput(path, sql));
        }

        return inputs;
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
              --stdin
              --stdin-filepath <path>
              --output <text|json>
              --severity <error|warning|info|hint>
              --preset <recommended|strict|security-only>
              --compat-level <110|120|130|140|150|160>
              --ruleset <path>
              --write                      (format only)
              --diff                       (format only)

          -h, --help
          -v, --version
        """;
}
