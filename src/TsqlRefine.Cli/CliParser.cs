using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public static class CliParser
{
    public static CliArgs Parse(string[] args)
    {
        var tokens = args?.ToList() ?? new List<string>();
        var showHelp = tokens.Contains("--help") || tokens.Contains("-h") || tokens.Contains("/?");
        var showVersion = tokens.Contains("--version") || tokens.Contains("-v");

        var command = tokens.Count > 0 && IsCommand(tokens[0]) ? tokens[0] : "lint";
        if (tokens.Count > 0 && IsCommand(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        var configPath = (string?)null;
        var ignorePath = (string?)null;
        var stdin = false;
        var stdinFilePath = (string?)null;
        var output = "text";
        DiagnosticSeverity? severity = null;
        var preset = (string?)null;
        int? compatLevel = null;
        var rulesetPath = (string?)null;
        var write = false;
        var diff = false;
        IndentStyle? indentStyle = null;
        int? indentSize = null;
        var paths = new List<string>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];

            if (t is "-i" or "--init")
            {
                command = "init";
                continue;
            }

            if (t is "-p" or "--print-config")
            {
                command = "print-config";
                continue;
            }

            if (t is "-l" or "--list-plugins")
            {
                command = "list-plugins";
                continue;
            }

            if (t is "-c" or "--config")
            {
                configPath = NextValue(tokens, ref i);
                continue;
            }

            if (t is "-g" or "--ignorelist")
            {
                ignorePath = NextValue(tokens, ref i);
                continue;
            }

            if (t is "--stdin")
            {
                stdin = true;
                continue;
            }

            if (t is "--stdin-filepath")
            {
                stdinFilePath = NextValue(tokens, ref i);
                continue;
            }

            if (t is "--output")
            {
                output = NextValue(tokens, ref i) ?? "text";
                continue;
            }

            if (t is "--severity")
            {
                severity = ParseSeverity(NextValue(tokens, ref i));
                continue;
            }

            if (t is "--preset")
            {
                preset = NextValue(tokens, ref i);
                continue;
            }

            if (t is "--compat-level")
            {
                if (int.TryParse(NextValue(tokens, ref i), out var v))
                {
                    compatLevel = v;
                }

                continue;
            }

            if (t is "--ruleset")
            {
                rulesetPath = NextValue(tokens, ref i);
                continue;
            }

            if (t is "--write")
            {
                write = true;
                continue;
            }

            if (t is "--diff")
            {
                diff = true;
                continue;
            }

            if (t is "--indent-style")
            {
                indentStyle = ParseIndentStyle(NextValue(tokens, ref i));
                continue;
            }

            if (t is "--indent-size")
            {
                if (int.TryParse(NextValue(tokens, ref i), out var n))
                {
                    indentSize = n;
                }

                continue;
            }

            if (t.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            paths.Add(t);
        }

        return new CliArgs(
            Command: command,
            ShowHelp: showHelp,
            ShowVersion: showVersion,
            ConfigPath: configPath,
            IgnoreListPath: ignorePath,
            Stdin: stdin,
            StdinFilePath: stdinFilePath,
            Output: output,
            MinimumSeverity: severity,
            Preset: preset,
            CompatLevel: compatLevel,
            RulesetPath: rulesetPath,
            Write: write,
            Diff: diff,
            IndentStyle: indentStyle,
            IndentSize: indentSize,
            Paths: paths
        );
    }

    private static bool IsCommand(string token) =>
        token is "lint" or "check" or "format" or "fix" or "init" or "print-config" or "list-rules" or "list-plugins";

    private static string? NextValue(IReadOnlyList<string> tokens, ref int index)
    {
        if (index + 1 >= tokens.Count)
        {
            return null;
        }

        index++;
        return tokens[index];
    }

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
}

