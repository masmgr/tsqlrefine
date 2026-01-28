using System.CommandLine;
using TsqlRefine.Formatting;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli;

public static class CliParser
{
    private static readonly ParserModel Model = BuildModel();

    public static CliArgs Parse(string[] args)
    {
        var parseResult = Model.Root.Parse(args ?? Array.Empty<string>());
        var command = GetCommandName(parseResult);
        var commandOverride = GetCommandOverride(parseResult);
        if (commandOverride is not null)
        {
            command = commandOverride;
        }

        var configPath = parseResult.GetValue(Model.ConfigOption);
        var ignorePath = parseResult.GetValue(Model.IgnoreListOption);
        var detectEncoding = parseResult.GetValue(Model.DetectEncodingOption);
        var stdin = parseResult.GetValue(Model.StdinOption);
        var stdinFilePath = parseResult.GetValue(Model.StdinFilePathOption);
        var output = parseResult.GetValue(Model.OutputOption) ?? "text";
        var severity = ParseSeverity(parseResult.GetValue(Model.SeverityOption));
        var preset = parseResult.GetValue(Model.PresetOption);
        var compatLevel = ParseInt(parseResult.GetValue(Model.CompatLevelOption));
        var rulesetPath = parseResult.GetValue(Model.RulesetOption);
        var write = parseResult.GetValue(Model.WriteOption);
        var diff = parseResult.GetValue(Model.DiffOption);
        var indentStyle = ParseIndentStyle(parseResult.GetValue(Model.IndentStyleOption));
        var indentSize = ParseInt(parseResult.GetValue(Model.IndentSizeOption));
        var verbose = parseResult.GetValue(Model.VerboseOption);
        var paths = GetPaths(parseResult);

        return new CliArgs(
            Command: command,
            ShowHelp: parseResult.GetValue(Model.HelpOption),
            ShowVersion: parseResult.GetValue(Model.VersionOption),
            ConfigPath: configPath,
            IgnoreListPath: ignorePath,
            DetectEncoding: detectEncoding,
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
            Verbose: verbose,
            Paths: paths
        );
    }

    private static string GetCommandName(ParseResult parseResult) =>
        parseResult.CommandResult.Command is RootCommand ? "lint" : parseResult.CommandResult.Command.Name;

    private static string? GetCommandOverride(ParseResult parseResult)
    {
        if (parseResult.GetValue(Model.InitOption))
        {
            return "init";
        }

        if (parseResult.GetValue(Model.PrintConfigOption))
        {
            return "print-config";
        }

        if (parseResult.GetValue(Model.ListPluginsOption))
        {
            return "list-plugins";
        }

        return null;
    }

    private static List<string> GetPaths(ParseResult parseResult)
    {
        if (parseResult.CommandResult.Command is RootCommand)
        {
            return (parseResult.GetValue(Model.RootPathsArgument) ?? Array.Empty<string>()).ToList();
        }

        if (Model.PathsByCommand.TryGetValue(parseResult.CommandResult.Command.Name, out var argument))
        {
            return (parseResult.GetValue(argument) ?? Array.Empty<string>()).ToList();
        }

        return new List<string>();
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

    private static ParserModel BuildModel()
    {
        var root = new RootCommand();

        var configOption = CreateOptionalStringOption("--config", "-c");
        var ignoreListOption = CreateOptionalStringOption("--ignorelist", "-g");
        var detectEncodingOption = CreateBoolOption("--detect-encoding");
        var stdinOption = CreateBoolOption("--stdin");
        var stdinFilePathOption = CreateOptionalStringOption("--stdin-filepath");
        var outputOption = CreateOptionalStringOption("--output");
        var severityOption = CreateOptionalStringOption("--severity");
        var presetOption = CreateOptionalStringOption("--preset");
        var compatLevelOption = CreateOptionalStringOption("--compat-level");
        var rulesetOption = CreateOptionalStringOption("--ruleset");
        var writeOption = CreateBoolOption("--write");
        var diffOption = CreateBoolOption("--diff");
        var indentStyleOption = CreateOptionalStringOption("--indent-style");
        var indentSizeOption = CreateOptionalStringOption("--indent-size");
        var helpOption = CreateBoolOption("--help", "-h", "/?");
        var versionOption = CreateBoolOption("--version", "-v");
        var initOption = CreateBoolOption("--init", "-i");
        var printConfigOption = CreateBoolOption("--print-config", "-p");
        var listPluginsOption = CreateBoolOption("--list-plugins", "-l");
        var verboseOption = CreateBoolOption("--verbose");

        root.Add(configOption);
        root.Add(ignoreListOption);
        root.Add(detectEncodingOption);
        root.Add(stdinOption);
        root.Add(stdinFilePathOption);
        root.Add(outputOption);
        root.Add(severityOption);
        root.Add(presetOption);
        root.Add(compatLevelOption);
        root.Add(rulesetOption);
        root.Add(writeOption);
        root.Add(diffOption);
        root.Add(indentStyleOption);
        root.Add(indentSizeOption);
        root.Add(helpOption);
        root.Add(versionOption);
        root.Add(initOption);
        root.Add(printConfigOption);
        root.Add(listPluginsOption);
        root.Add(verboseOption);

        var rootPaths = CreatePathsArgument();
        root.Add(rootPaths);

        var pathsByCommand = new Dictionary<string, Argument<string[]>>(StringComparer.Ordinal);
        foreach (var commandName in new[]
                 {
                     "lint",
                     "check",
                     "format",
                     "fix",
                     "init",
                     "print-config",
                     "list-rules",
                     "list-plugins"
                 })
        {
            var command = new Command(commandName);
            var paths = CreatePathsArgument();
            command.Add(paths);
            root.Add(command);
            pathsByCommand[commandName] = paths;
        }

        return new ParserModel(
            Root: root,
            ConfigOption: configOption,
            IgnoreListOption: ignoreListOption,
            DetectEncodingOption: detectEncodingOption,
            StdinOption: stdinOption,
            StdinFilePathOption: stdinFilePathOption,
            OutputOption: outputOption,
            SeverityOption: severityOption,
            PresetOption: presetOption,
            CompatLevelOption: compatLevelOption,
            RulesetOption: rulesetOption,
            WriteOption: writeOption,
            DiffOption: diffOption,
            IndentStyleOption: indentStyleOption,
            IndentSizeOption: indentSizeOption,
            HelpOption: helpOption,
            VersionOption: versionOption,
            InitOption: initOption,
            PrintConfigOption: printConfigOption,
            ListPluginsOption: listPluginsOption,
            VerboseOption: verboseOption,
            RootPathsArgument: rootPaths,
            PathsByCommand: pathsByCommand
        );
    }

    private static Option<string?> CreateOptionalStringOption(string name, params string[] aliases)
    {
        var option = new Option<string?>(name, aliases) { Arity = ArgumentArity.ZeroOrOne };
        option.Recursive = true;
        return option;
    }

    private static Option<bool> CreateBoolOption(string name, params string[] aliases)
    {
        var option = new Option<bool>(name, aliases);
        option.Recursive = true;
        return option;
    }

    private static Argument<string[]> CreatePathsArgument() =>
        new("paths") { Arity = ArgumentArity.ZeroOrMore };

    private sealed record ParserModel(
        RootCommand Root,
        Option<string?> ConfigOption,
        Option<string?> IgnoreListOption,
        Option<bool> DetectEncodingOption,
        Option<bool> StdinOption,
        Option<string?> StdinFilePathOption,
        Option<string?> OutputOption,
        Option<string?> SeverityOption,
        Option<string?> PresetOption,
        Option<string?> CompatLevelOption,
        Option<string?> RulesetOption,
        Option<bool> WriteOption,
        Option<bool> DiffOption,
        Option<string?> IndentStyleOption,
        Option<string?> IndentSizeOption,
        Option<bool> HelpOption,
        Option<bool> VersionOption,
        Option<bool> InitOption,
        Option<bool> PrintConfigOption,
        Option<bool> ListPluginsOption,
        Option<bool> VerboseOption,
        Argument<string[]> RootPathsArgument,
        IReadOnlyDictionary<string, Argument<string[]>> PathsByCommand);
}
