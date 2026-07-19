using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Core;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Metrics;

namespace TsqlRefine.Cli.Services;

public sealed record ReportSummary(
    int FileCount,
    int DiagnosticCount,
    int ErrorCount,
    int WarningCount,
    int InformationCount,
    int HintCount);

public sealed record ReportCount(string Name, int Count);

public sealed record ReportMetric(
    string FilePath,
    string Name,
    string Kind,
    int Line,
    int CyclomaticComplexity,
    int NestingDepth,
    int StatementCount,
    int MaxJoinsPerQuery,
    int ParameterCount);

public sealed record ReportBaselineSummary(int NewCount, int FrozenCount, int ResolvedCount);

public sealed record ReportDocument(
    int SchemaVersion,
    string Tool,
    string Version,
    DateTimeOffset GeneratedAt,
    ReportSummary Summary,
    IReadOnlyList<ReportCount> DiagnosticsByCategory,
    IReadOnlyList<ReportCount> DiagnosticsByRule,
    IReadOnlyList<ReportCount> DiagnosticsByFile,
    IReadOnlyList<ReportMetric> TopComplexObjects,
    ReportBaselineSummary? Baseline);

public static class ReportWriter
{
    public const int CurrentSchemaVersion = 1;
    private const int MaximumRankedObjects = 20;

    public static ReportDocument Create(
        LintResult result,
        BaselineClassification classification,
        IReadOnlyList<SqlInput> inputs,
        int compatLevel,
        bool baselineApplied)
    {
        var diagnostics = classification.Files
            .SelectMany(file => file.Diagnostics.Select(item => (file.FilePath, Item: item)))
            .ToArray();
        var plainDiagnostics = diagnostics.Select(item => item.Item.Diagnostic).ToArray();
        var reportMetrics = inputs
            .SelectMany(input => CollectMetrics(input, compatLevel))
            .OrderByDescending(metric => metric.CyclomaticComplexity)
            .ThenByDescending(metric => metric.StatementCount)
            .ThenBy(metric => metric.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(metric => metric.Name, StringComparer.Ordinal)
            .Take(MaximumRankedObjects)
            .ToArray();

        return new ReportDocument(
            CurrentSchemaVersion,
            result.Tool,
            result.Version,
            DateTimeOffset.UtcNow,
            new ReportSummary(
                result.Files.Count,
                plainDiagnostics.Length,
                CountSeverity(plainDiagnostics, DiagnosticSeverity.Error),
                CountSeverity(plainDiagnostics, DiagnosticSeverity.Warning),
                CountSeverity(plainDiagnostics, DiagnosticSeverity.Information),
                CountSeverity(plainDiagnostics, DiagnosticSeverity.Hint)),
            Aggregate(plainDiagnostics.Select(diagnostic => diagnostic.Data?.Category ?? "Analysis")),
            Aggregate(plainDiagnostics.Select(diagnostic => diagnostic.Data?.RuleId ?? diagnostic.Code ?? "unknown")),
            Aggregate(diagnostics.Select(item => item.FilePath)),
            reportMetrics,
            baselineApplied
                ? new ReportBaselineSummary(
                    diagnostics.Count(item =>
                        !item.Item.Suppressed && !BaselineStore.IsAnalysisFailure(item.Item.Diagnostic)),
                    diagnostics.Count(item => item.Item.Suppressed),
                    classification.ResolvedCount)
                : null);
    }

    public static async Task WriteAsync(
        ReportDocument report,
        string format,
        string? outputPath,
        TextWriter stdout)
    {
        var content = string.Equals(format, "html", StringComparison.OrdinalIgnoreCase)
            ? CreateHtml(report)
            : JsonSerializer.Serialize(report, JsonDefaults.Options) + Environment.NewLine;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await stdout.WriteAsync(content);
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, new UTF8Encoding(false));
    }

    private static ReportMetric[] CollectMetrics(SqlInput input, int compatLevel)
    {
        var parser = CreateParser(compatLevel);
        using var reader = new StringReader(input.Text);
        var fragment = parser.Parse(reader, out _);
        if (fragment is null)
        {
            return [];
        }

        return SqlMetricsCollector.Collect(fragment)
            .Select(metric => new ReportMetric(
                input.FilePath,
                metric.Name,
                metric.Kind,
                Math.Max(1, metric.Location.StartLine),
                metric.CyclomaticComplexity,
                metric.NestingDepth,
                metric.StatementCount,
                metric.MaxJoinsPerQuery,
                metric.ParameterCount))
            .ToArray();
    }

    private static TSqlParser CreateParser(int compatLevel) => compatLevel switch
    {
        >= 160 => new TSql160Parser(initialQuotedIdentifiers: true),
        >= 150 => new TSql150Parser(initialQuotedIdentifiers: true),
        >= 140 => new TSql140Parser(initialQuotedIdentifiers: true),
        >= 130 => new TSql130Parser(initialQuotedIdentifiers: true),
        >= 120 => new TSql120Parser(initialQuotedIdentifiers: true),
        >= 110 => new TSql110Parser(initialQuotedIdentifiers: true),
        _ => new TSql100Parser(initialQuotedIdentifiers: true)
    };

    private static ReportCount[] Aggregate(IEnumerable<string> names) => names
        .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ReportCount(group.Key, group.Count()))
        .OrderByDescending(item => item.Count)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static int CountSeverity(IEnumerable<Diagnostic> diagnostics, DiagnosticSeverity severity) =>
        diagnostics.Count(diagnostic => diagnostic.Severity == severity);

    private static string CreateHtml(ReportDocument report)
    {
        var builder = new StringBuilder();
        builder.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>tsqlrefine report</title><style>")
            .Append("body{font:14px system-ui,sans-serif;margin:2rem;color:#18212f;background:#f7f8fa}")
            .Append("main{max-width:1200px;margin:auto}h1,h2{color:#152a46}.cards{display:flex;gap:1rem;flex-wrap:wrap}")
            .Append(".card,section{background:white;border:1px solid #dce2ea;border-radius:8px;padding:1rem;margin:1rem 0}")
            .Append(".card strong{font-size:1.6rem;display:block}table{width:100%;border-collapse:collapse}")
            .Append("th,td{text-align:left;padding:.5rem;border-bottom:1px solid #e6e9ee}th{background:#edf2f7}")
            .Append("input{padding:.5rem;width:min(28rem,100%);border:1px solid #bbc5d1;border-radius:4px}")
            .Append("</style></head><body><main><h1>tsqlrefine report</h1><p>Generated ")
            .Append(Html(report.GeneratedAt.ToString("u", System.Globalization.CultureInfo.InvariantCulture)))
            .Append("</p><div class=\"cards\">");
        AppendCard(builder, "Files", report.Summary.FileCount);
        AppendCard(builder, "Diagnostics", report.Summary.DiagnosticCount);
        AppendCard(builder, "Errors", report.Summary.ErrorCount);
        AppendCard(builder, "Warnings", report.Summary.WarningCount);
        if (report.Baseline is not null)
        {
            AppendCard(builder, "New", report.Baseline.NewCount);
            AppendCard(builder, "Frozen", report.Baseline.FrozenCount);
            AppendCard(builder, "Resolved", report.Baseline.ResolvedCount);
        }
        builder.Append("</div><section><label>Filter tables <input id=\"filter\" type=\"search\" placeholder=\"rule, file, or object\"></label></section>");
        AppendCountsTable(builder, "Diagnostics by category", report.DiagnosticsByCategory);
        AppendCountsTable(builder, "Diagnostics by rule", report.DiagnosticsByRule);
        AppendCountsTable(builder, "Diagnostics by file", report.DiagnosticsByFile);
        AppendMetricsTable(builder, report.TopComplexObjects);
        builder.Append("</main><script>")
            .Append("document.getElementById('filter').addEventListener('input',e=>{const q=e.target.value.toLowerCase();document.querySelectorAll('tbody tr').forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(q));});")
            .Append("</script></body></html>");
        return builder.ToString();
    }

    private static void AppendCard(StringBuilder builder, string label, int value) => builder
        .Append("<div class=\"card\"><strong>").Append(value).Append("</strong>")
        .Append(Html(label)).Append("</div>");

    private static void AppendCountsTable(StringBuilder builder, string title, IReadOnlyList<ReportCount> counts)
    {
        builder.Append("<section><h2>").Append(Html(title)).Append("</h2><table><thead><tr><th>Name</th><th>Count</th></tr></thead><tbody>");
        foreach (var item in counts)
        {
            builder.Append("<tr><td>").Append(Html(item.Name)).Append("</td><td>").Append(item.Count).Append("</td></tr>");
        }
        builder.Append("</tbody></table></section>");
    }

    private static void AppendMetricsTable(StringBuilder builder, IReadOnlyList<ReportMetric> metrics)
    {
        builder.Append("<section><h2>Top complex objects</h2><table><thead><tr><th>File</th><th>Object</th><th>Kind</th><th>Complexity</th><th>Nesting</th><th>Statements</th><th>Joins</th><th>Parameters</th></tr></thead><tbody>");
        foreach (var metric in metrics)
        {
            builder.Append("<tr><td>").Append(Html(metric.FilePath)).Append(':').Append(metric.Line)
                .Append("</td><td>").Append(Html(metric.Name)).Append("</td><td>").Append(Html(metric.Kind))
                .Append("</td><td>").Append(metric.CyclomaticComplexity).Append("</td><td>").Append(metric.NestingDepth)
                .Append("</td><td>").Append(metric.StatementCount).Append("</td><td>").Append(metric.MaxJoinsPerQuery)
                .Append("</td><td>").Append(metric.ParameterCount).Append("</td></tr>");
        }
        builder.Append("</tbody></table></section>");
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
