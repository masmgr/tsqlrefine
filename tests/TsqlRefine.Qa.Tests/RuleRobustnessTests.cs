using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

/// <summary>
/// Verifies that every built-in rule tolerates degenerate inputs without throwing:
/// empty or comment-only files, unparseable SQL, and a missing AST fragment.
/// The engine converts rule exceptions into "crashed" diagnostics at runtime,
/// so any throw here is a latent bug that would surface to users as a crash report.
/// </summary>
public sealed class RuleRobustnessTests
{
    private static readonly (string Name, string Sql)[] EdgeInputs =
    [
        ("empty", ""),
        ("whitespace-only", "   \n\t \n"),
        ("line-comment-only", "-- just a comment\n"),
        ("block-comment-only", "/* block\ncomment */"),
        ("go-only", "GO\nGO\n"),
        ("parse-error", "SELEC * FRM t WHERE"),
        ("unterminated-string", "SELECT '"),
        ("unterminated-block-comment", "SELECT 1 /* never closed"),
        ("multiline-string", "SELECT 'a\nb' AS v FROM t;"),
        ("trailing-multiline-comment", "SELECT 1 AS v /* spans\nmultiple\nlines */"),
        ("semicolon-only", ";;;"),
        ("unicode-identifiers", "SELECT N'テスト' AS [名前];")
    ];

    [Fact]
    public void EveryBuiltinRule_OnEdgeInputs_DoesNotThrowAndEmitsValidDiagnostics()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var failures = new List<string>();

        foreach (var (name, sql) in EdgeInputs)
        {
            var path = $"<edge:{name}>";
            var context = RuleRunSupport.CreateContext(path, sql, 160);
            var lines = RuleRunSupport.SplitLines(sql);
            RunAllRules(rules, context, lines, path, failures);
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} robustness violation(s):{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(80))}");
    }

    [Fact]
    public void EveryBuiltinRule_WithMissingAstFragment_DoesNotThrow()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var failures = new List<string>();
        const string sql = "SELECT 1;";
        const string path = "<edge:null-fragment>";

        var context = new RuleContext(
            FilePath: path,
            CompatLevel: 160,
            Ast: new ScriptDomAst(sql),
            Tokens: [],
            Settings: new RuleSettings());

        RunAllRules(rules, context, RuleRunSupport.SplitLines(sql), path, failures);

        Assert.True(failures.Count == 0,
            $"{failures.Count} robustness violation(s):{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(80))}");
    }

    private static void RunAllRules(
        IReadOnlyList<IRule> rules,
        RuleContext context,
        IReadOnlyList<string> lines,
        string path,
        List<string> failures)
    {
        foreach (var rule in rules)
        {
            IReadOnlyList<Diagnostic> diagnostics;
            try
            {
                diagnostics = rule.Analyze(context).ToArray();
            }
#pragma warning disable CA1031 // Any rule exception is itself the failure being reported.
            catch (Exception ex)
            {
                failures.Add($"{rule.Metadata.RuleId} threw on '{path}': {ex.GetType().Name}: {ex.Message}");
                continue;
            }
#pragma warning restore CA1031

            foreach (var diagnostic in diagnostics)
            {
                RuleDiagnosticValidator.ValidateDiagnostic(rule.Metadata, diagnostic, lines, path, failures);

                if (rule.Metadata.Fixable && diagnostic.Data?.Fixable is not false)
                {
                    RuleDiagnosticValidator.ValidateFixes(rule, context, diagnostic, lines, path, failures);
                }
            }
        }
    }
}
