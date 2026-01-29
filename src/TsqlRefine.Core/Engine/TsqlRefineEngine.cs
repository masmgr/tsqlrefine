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

        var activeRules = GetActiveRules(options);
        var inputList = inputs as IList<SqlInput> ?? inputs.ToList();
        var files = new FileResult[inputList.Count];
        for (var i = 0; i < inputList.Count; i++)
        {
            files[i] = AnalyzeFile(inputList[i], activeRules, options);
        }
        return new LintResult(
            Tool: "tsqlrefine",
            Version: GetVersion(),
            Command: command,
            Files: files
        );
    }

    public FixResult Fix(IEnumerable<SqlInput> inputs, EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);

        var activeRules = GetActiveRules(options);
        var inputList = inputs as IList<SqlInput> ?? inputs.ToList();
        var files = new FixedFileResult[inputList.Count];
        for (var i = 0; i < inputList.Count; i++)
        {
            files[i] = FixFile(inputList[i], activeRules, options);
        }
        return new FixResult(
            Tool: "tsqlrefine",
            Version: GetVersion(),
            Command: "fix",
            Files: files
        );
    }

    private static FileResult AnalyzeFile(SqlInput input, IReadOnlyList<IRule> rules, EngineOptions options)
    {
        var diagnostics = new List<Diagnostic>();
        var context = CreateContext(input, options);

        foreach (var rule in rules)
        {
            AppendDiagnostics(rule, context, options, diagnostics, fixGroups: null);
        }

        return new FileResult(input.FilePath, diagnostics);
    }

    private static FixedFileResult FixFile(SqlInput input, IReadOnlyList<IRule> rules, EngineOptions options)
    {
        var context = CreateContext(input, options);

        var diagnostics = new List<Diagnostic>();
        var fixGroups = new List<DiagnosticFixGroup>();

        foreach (var rule in rules)
        {
            AppendDiagnostics(rule, context, options, diagnostics, fixGroups);
        }

        var fixOutcome = FixApplier.ApplyFixes(input.Text, fixGroups);
        var fixedInput = new SqlInput(input.FilePath, fixOutcome.FixedText);
        var finalDiagnostics = AnalyzeFile(fixedInput, rules, options).Diagnostics;

        return new FixedFileResult(
            FilePath: input.FilePath,
            OriginalText: input.Text,
            FixedText: fixOutcome.FixedText,
            Diagnostics: finalDiagnostics,
            AppliedFixes: fixOutcome.AppliedFixes,
            SkippedFixes: fixOutcome.SkippedFixes
        );
    }


    private static Diagnostic NormalizeDiagnostic(IRule rule, Diagnostic diagnostic)
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

    private IReadOnlyList<IRule> GetActiveRules(EngineOptions options)
    {
        var ruleset = options.Ruleset;
        return ruleset is null
            ? _rules
            : _rules.Where(r => ruleset.IsRuleEnabled(r.Metadata.RuleId)).ToArray();
    }

    private static readonly TsqlRefine.PluginSdk.Range ZeroRange =
        new(new Position(0, 0), new Position(0, 0));

    private static RuleContext CreateContext(SqlInput input, EngineOptions options)
    {
        var ruleSettings = options.RuleSettings ?? new RuleSettings();
        var analysis = ScriptDomTokenizer.Analyze(input.Text, options.CompatLevel);
        return new RuleContext(
            FilePath: input.FilePath,
            CompatLevel: options.CompatLevel,
            Ast: analysis.Ast,
            Tokens: analysis.Tokens,
            Settings: ruleSettings
        );
    }

    private static void AppendDiagnostics(
        IRule rule,
        RuleContext context,
        EngineOptions options,
        List<Diagnostic> diagnostics,
        List<DiagnosticFixGroup>? fixGroups)
    {
#pragma warning disable CA1031 // We intentionally isolate rule failures into diagnostics.
        try
        {
            foreach (var diagnostic in rule.Analyze(context) ?? Array.Empty<Diagnostic>())
            {
                var normalized = NormalizeDiagnostic(rule, diagnostic);
                if (!IsAtOrAbove(normalized.Severity ?? DiagnosticSeverity.Warning, options.MinimumSeverity))
                {
                    continue;
                }

                diagnostics.Add(normalized);

                if (fixGroups is null)
                {
                    continue;
                }

                if (normalized.Data?.Fixable is true && rule.Metadata.Fixable)
                {
                    try
                    {
                        var fixes = (rule.GetFixes(context, normalized) ?? Array.Empty<Fix>())
                            .Where(f => f.Edits is not null && f.Edits.Count > 0)
                            .ToArray();
                        if (fixes.Length > 0)
                        {
                            fixGroups.Add(new DiagnosticFixGroup(rule, normalized, fixes));
                        }
                    }
                    catch (Exception ex)
                    {
                        diagnostics.Add(CreateRuleExceptionDiagnostic(rule, ex, isFix: true));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(CreateRuleExceptionDiagnostic(rule, ex, isFix: false));
        }
#pragma warning restore CA1031
    }

    private static Diagnostic CreateRuleExceptionDiagnostic(IRule rule, Exception ex, bool isFix)
    {
        var suffix = isFix ? "fix crashed" : "crashed";
        return new Diagnostic(
            Range: ZeroRange,
            Message: $"Rule '{rule.Metadata.RuleId}' {suffix}: {ex.GetType().Name}: {ex.Message}",
            Severity: DiagnosticSeverity.Error,
            Code: rule.Metadata.RuleId,
            Data: new DiagnosticData(rule.Metadata.RuleId, rule.Metadata.Category, rule.Metadata.Fixable)
        );
    }

}
