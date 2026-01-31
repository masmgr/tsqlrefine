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

        // Convert parse errors to diagnostics
        AppendParseErrors(context.Ast, diagnostics);

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

        // Convert parse errors to diagnostics
        AppendParseErrors(context.Ast, diagnostics);

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
        var metadata = rule.Metadata;
        var originalData = diagnostic.Data;

        // Build DiagnosticData with defaults from rule metadata
        var normalizedData = new DiagnosticData(
            RuleId: originalData?.RuleId ?? metadata.RuleId,
            Category: originalData?.Category ?? metadata.Category,
            Fixable: originalData?.Fixable ?? metadata.Fixable
        );

        return diagnostic with
        {
            Severity = diagnostic.Severity ?? Map(metadata.DefaultSeverity),
            Code = diagnostic.Code ?? metadata.RuleId,
            Data = normalizedData
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

    public const string ParseErrorCode = "parse-error";

    public const string ParserExceptionCode = "parser-exception";

    private static void AppendParseErrors(ScriptDomAst ast, List<Diagnostic> diagnostics)
    {
        // Report parser exception if any
        if (ast.ParserException is not null)
        {
            var ex = ast.ParserException;
            diagnostics.Add(new Diagnostic(
                Range: ZeroRange,
                Message: $"Parser exception: {ex.GetType().Name}: {ex.Message}",
                Severity: DiagnosticSeverity.Error,
                Code: ParserExceptionCode,
                Data: new DiagnosticData(ParserExceptionCode, "Syntax", false)
            ));
        }

        foreach (var parseError in ast.ParseErrors)
        {
            diagnostics.Add(CreateParseErrorDiagnostic("Parse error", parseError));
        }

        foreach (var tokenError in ast.TokenizationErrors)
        {
            diagnostics.Add(CreateParseErrorDiagnostic("Tokenization error", tokenError));
        }
    }

    private static Diagnostic CreateParseErrorDiagnostic(string prefix, Microsoft.SqlServer.TransactSql.ScriptDom.ParseError error)
    {
        var line = Math.Max(0, error.Line - 1);
        var column = Math.Max(0, error.Column - 1);
        return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(
                new Position(line, column),
                new Position(line, column)),
            Message: $"{prefix}: {error.Message}",
            Severity: DiagnosticSeverity.Error,
            Code: ParseErrorCode,
            Data: new DiagnosticData(ParseErrorCode, "Syntax", false)
        );
    }

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
