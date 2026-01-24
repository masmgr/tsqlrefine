using System.Reflection;
using TsqlRefine.Core.Config;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Engine;

public sealed class TsqlRefineEngine
{
    private readonly IReadOnlyList<IRule> _rules;

    public TsqlRefineEngine(IEnumerable<IRule> rules)
    {
        _rules = rules?.ToArray() ?? Array.Empty<IRule>();
    }

    public LintResult Run(string command, IEnumerable<SqlInput> inputs, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);

        var ruleset = options.Ruleset;
        var activeRules = ruleset is null
            ? _rules
            : _rules.Where(r => ruleset.IsRuleEnabled(r.Metadata.RuleId)).ToArray();

        var files = inputs.Select(i => AnalyzeFile(i, activeRules, options)).ToArray();
        return new LintResult(
            Tool: "tsqlrefine",
            Version: GetVersion(),
            Command: command,
            Files: files
        );
    }

    private static FileResult AnalyzeFile(SqlInput input, IReadOnlyList<IRule> rules, EngineOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var ruleSettings = options.RuleSettings ?? new RuleSettings();

        var analysis = ScriptDomTokenizer.Analyze(input.Text, options.CompatLevel);
        var context = new RuleContext(
            FilePath: input.FilePath,
            CompatLevel: options.CompatLevel,
            Ast: analysis.Ast,
            Tokens: analysis.Tokens,
            Settings: ruleSettings
        );

        foreach (var rule in rules)
        {
            try
            {
                foreach (var diagnostic in rule.Analyze(context) ?? Array.Empty<Diagnostic>())
                {
                    var normalized = NormalizeDiagnostic(rule, diagnostic, options);
                    if (IsAtOrAbove(normalized.Severity ?? DiagnosticSeverity.Warning, options.MinimumSeverity))
                    {
                        diagnostics.Add(normalized);
                    }
                }
            }
#pragma warning disable CA1031 // We intentionally isolate rule failures into diagnostics.
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 0)),
                    Message: $"Rule '{rule.Metadata.RuleId}' crashed: {ex.GetType().Name}: {ex.Message}",
                    Severity: DiagnosticSeverity.Error,
                    Code: rule.Metadata.RuleId,
                    Data: new DiagnosticData(rule.Metadata.RuleId, rule.Metadata.Category, rule.Metadata.Fixable)
                ));
            }
#pragma warning restore CA1031
        }

        return new FileResult(input.FilePath, diagnostics);
    }

    private static Diagnostic NormalizeDiagnostic(IRule rule, Diagnostic diagnostic, EngineOptions options)
    {
        var data = diagnostic.Data ?? new DiagnosticData();
        if (data.RuleId is null || data.Category is null || data.Fixable is null)
        {
            data = data with
            {
                RuleId = data.RuleId ?? rule.Metadata.RuleId,
                Category = data.Category ?? rule.Metadata.Category,
                Fixable = data.Fixable ?? rule.Metadata.Fixable
            };
        }

        var severity = diagnostic.Severity ?? Map(rule.Metadata.DefaultSeverity);
        return diagnostic with
        {
            Severity = severity,
            Code = diagnostic.Code ?? rule.Metadata.RuleId,
            Data = data
        };
    }

    private static DiagnosticSeverity Map(RuleSeverity severity) =>
        severity switch
        {
            RuleSeverity.Error => DiagnosticSeverity.Error,
            RuleSeverity.Warning => DiagnosticSeverity.Warning,
            RuleSeverity.Information => DiagnosticSeverity.Information,
            RuleSeverity.Hint => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Warning
        };

    private static bool IsAtOrAbove(DiagnosticSeverity value, DiagnosticSeverity threshold) =>
        value <= threshold;

    private static string GetVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetName().Version?.ToString() ?? "0.0.0";
    }
}
