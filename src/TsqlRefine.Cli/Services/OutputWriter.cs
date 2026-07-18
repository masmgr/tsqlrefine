using System.Text.Json;
using System.Text.Json.Nodes;
using TsqlRefine.Core;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Cli.Services;

/// <summary>
/// Writes output in various formats (text, JSON) to stdout/stderr.
/// </summary>
public sealed class OutputWriter
{
    private const string SarifSchema = "https://json.schemastore.org/sarif-2.1.0.json";

    public static async Task<int> WriteErrorAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return ExitCodes.Fatal;
    }

    public static async Task WriteJsonOutputAsync<T>(TextWriter stdout, T data)
    {
        await stdout.WriteLineAsync(JsonSerializer.Serialize(data, JsonDefaults.Options));
    }

    public static Task WriteClassifiedJsonOutputAsync(
        TextWriter stdout,
        string version,
        string command,
        IReadOnlyList<ClassifiedFile> files)
    {
        var result = new LintOutputResult(
            "tsqlrefine",
            version,
            command,
            files.Select(file => new LintOutputFile(
                file.FilePath,
                file.Diagnostics.Select(item => OutputDiagnostic.From(item)).ToArray())).ToArray());
        return WriteJsonOutputAsync(stdout, result);
    }

    public static async Task WriteSarifOutputAsync(
        TextWriter stdout,
        string version,
        IReadOnlyList<ClassifiedFile> files,
        IReadOnlyList<IRule> rules,
        string root,
        bool includeSuppressed)
    {
        var metadata = rules.ToDictionary(rule => rule.Metadata.RuleId, StringComparer.Ordinal);
        var included = files
            .SelectMany(file => file.Diagnostics.Select(item => (file.FilePath, Item: item)))
            .Where(item => includeSuppressed || !item.Item.Suppressed)
            .ToArray();
        var ruleIds = included
            .Select(item => item.Item.Diagnostic.Data?.RuleId ?? item.Item.Diagnostic.Code ?? "unknown")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var ruleIndexes = ruleIds
            .Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);

        var sarifRules = new JsonArray();
        foreach (var ruleId in ruleIds)
        {
            var rule = new JsonObject { ["id"] = ruleId };
            if (metadata.TryGetValue(ruleId, out var metadataRule))
            {
                var ruleMetadata = metadataRule.Metadata;
                rule["shortDescription"] = new JsonObject { ["text"] = ruleMetadata.Description };
                if (ruleMetadata.DocumentationUri is not null)
                {
                    rule["helpUri"] = ruleMetadata.DocumentationUri.ToString();
                }
                rule["defaultConfiguration"] = new JsonObject
                {
                    ["level"] = MapSarifLevel(MapSeverity(ruleMetadata.DefaultSeverity))
                };
                rule["properties"] = new JsonObject { ["category"] = ruleMetadata.Category };
            }
            sarifRules.Add(rule);
        }

        var artifacts = new JsonArray();
        foreach (var filePath in included.Select(item => item.FilePath).Distinct(GetPathComparer()).OrderBy(path => path, GetPathComparer()))
        {
            artifacts.Add(new JsonObject
            {
                ["location"] = new JsonObject { ["uri"] = ToArtifactUri(filePath, root) }
            });
        }

        var results = new JsonArray();
        foreach (var (filePath, item) in included)
        {
            var diagnostic = item.Diagnostic;
            var ruleId = diagnostic.Data?.RuleId ?? diagnostic.Code ?? "unknown";
            var region = new JsonObject
            {
                ["startLine"] = diagnostic.Range.Start.Line + 1,
                ["startColumn"] = diagnostic.Range.Start.Character + 1
            };
            if (diagnostic.Range.End.Line > diagnostic.Range.Start.Line ||
                diagnostic.Range.End.Character > diagnostic.Range.Start.Character)
            {
                region["endLine"] = diagnostic.Range.End.Line + 1;
                region["endColumn"] = diagnostic.Range.End.Character + 1;
            }

            var result = new JsonObject
            {
                ["ruleId"] = ruleId,
                ["ruleIndex"] = ruleIndexes[ruleId],
                ["level"] = MapSarifLevel(diagnostic.Severity ?? DiagnosticSeverity.Warning),
                ["message"] = new JsonObject { ["text"] = diagnostic.Message },
                ["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["physicalLocation"] = new JsonObject
                        {
                            ["artifactLocation"] = new JsonObject { ["uri"] = ToArtifactUri(filePath, root) },
                            ["region"] = region
                        }
                    }
                }
            };
            if (item.Fingerprint is not null)
            {
                result["partialFingerprints"] = new JsonObject
                {
                    ["tsqlrefine/v1"] = item.Fingerprint
                };
            }
            if (item.Suppressed)
            {
                result["suppressions"] = new JsonArray
                {
                    new JsonObject { ["kind"] = "external", ["justification"] = "Matched tsqlrefine baseline" }
                };
            }
            results.Add(result);
        }

        var document = new JsonObject
        {
            ["$schema"] = SarifSchema,
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = "tsqlrefine",
                            ["version"] = version,
                            ["informationUri"] = "https://github.com/masmgr/tsqlrefine",
                            ["rules"] = sarifRules
                        }
                    },
                    ["artifacts"] = artifacts,
                    ["results"] = results
                }
            }
        };
        await stdout.WriteLineAsync(document.ToJsonString(JsonDefaults.Options));
    }

    private static DiagnosticSeverity MapSeverity(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Error => DiagnosticSeverity.Error,
        RuleSeverity.Warning => DiagnosticSeverity.Warning,
        RuleSeverity.Information => DiagnosticSeverity.Information,
        RuleSeverity.Hint => DiagnosticSeverity.Hint,
        _ => DiagnosticSeverity.Warning
    };

    private static string MapSarifLevel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Information or DiagnosticSeverity.Hint => "note",
        _ => "none"
    };

    private static string ToArtifactUri(string filePath, string root)
    {
        if (string.Equals(filePath, "<stdin>", StringComparison.Ordinal))
        {
            return "stdin.sql";
        }
        var relative = Path.GetRelativePath(root, Path.GetFullPath(filePath));
        return string.Join(
            '/',
            relative.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

public sealed record LintOutputResult(
    string Tool,
    string Version,
    string Command,
    IReadOnlyList<LintOutputFile> Files);

public sealed record LintOutputFile(string FilePath, IReadOnlyList<OutputDiagnostic> Diagnostics);

public sealed record OutputDiagnostic(
    TsqlRefine.PluginSdk.Range Range,
    string Message,
    DiagnosticSeverity? Severity,
    string? Code,
    string Source,
    IReadOnlyList<DiagnosticTag>? Tags,
    DiagnosticData? Data,
    bool Suppressed,
    string? Fingerprint)
{
    public static OutputDiagnostic From(ClassifiedDiagnostic item) => new(
        item.Diagnostic.Range,
        item.Diagnostic.Message,
        item.Diagnostic.Severity,
        item.Diagnostic.Code,
        item.Diagnostic.Source,
        item.Diagnostic.Tags,
        item.Diagnostic.Data,
        item.Suppressed,
        item.Fingerprint);
}
