using System.Text.Json;
using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class CorpusLintSnapshotTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { WriteIndented = true };

    [Fact]
    public void Lint_AllCorpusFiles_HasNoUnhandledFailuresAndMatchesSnapshot()
    {
        var inputs = CorpusSupport.LoadFiles()
            .Select(file => new SqlInput(file.Path, CorpusSupport.Read(file)))
            .ToArray();
        var result = new TsqlRefineEngine(new BuiltinRuleProvider().GetRules())
            .Run("lint", inputs, new EngineOptions(CompatLevel: 160));

        var failures = result.Files.SelectMany(file => file.Diagnostics
            .Where(diagnostic => diagnostic.Code == TsqlRefineEngine.ParserExceptionCode ||
                diagnostic.Message.Contains(" crashed:", StringComparison.Ordinal))
            .Select(diagnostic => $"{file.FilePath}: {diagnostic.Message}"));
        Assert.Empty(failures);

        var snapshot = result.Files
            .SelectMany(file => file.Diagnostics.Select(diagnostic => new DiagnosticSnapshot(
                file.FilePath.Replace('\\', '/'),
                diagnostic.Data?.RuleId ?? diagnostic.Code ?? string.Empty,
                diagnostic.Severity?.ToString() ?? "None",
                diagnostic.Message,
                diagnostic.Range.Start.Line,
                diagnostic.Range.Start.Character,
                diagnostic.Range.End.Line,
                diagnostic.Range.End.Character)))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.StartLine)
            .ThenBy(item => item.StartCharacter)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();
        var json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions) + "\n";
        var snapshotPath = Path.Combine(CorpusSupport.CorpusRoot, "snapshots", "diagnostics.json");

        if (Environment.GetEnvironmentVariable("UPDATE_CORPUS_SNAPSHOTS") == "1")
        {
            File.WriteAllText(snapshotPath, json);
        }

        Assert.Equal(Normalize(File.ReadAllText(snapshotPath)), Normalize(json));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record DiagnosticSnapshot(
        string Path,
        string RuleId,
        string Severity,
        string Message,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter);
}
