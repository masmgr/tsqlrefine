using System.Collections.Frozen;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

/// <summary>
/// Cross-cutting invariants for every built-in rule, verified by running the rules
/// directly (without engine normalization) against the sample and corpus SQL files.
/// These tests catch structural mistakes that unit tests for individual rules miss:
/// diagnostics whose code/category/fixable flags drift from the rule metadata,
/// ranges that point outside the analyzed document, and rules that throw.
/// </summary>
public sealed class RuleDiagnosticIntegrityTests
{
    private static readonly FrozenSet<string> SchemaDependentRuleIds = new[]
    {
        "delete-column-not-in-table",
        "implicit-conversion-in-predicate-schema",
        "insert-column-not-in-table",
        "join-column-deviation",
        "join-foreign-key-mismatch",
        "unresolved-column-reference",
        "unresolved-table-reference",
        "update-column-not-in-table",
        "update-join-cardinality-mismatch"
    }.ToFrozenSet(StringComparer.Ordinal);

    [Fact]
    public void EveryBuiltinRule_OnSamplesAndCorpus_EmitsMetadataConsistentDiagnostics()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        var failures = new List<string>();
        var validatedRuleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, sql, compatLevel) in RuleRunSupport.EnumerateSampleAndCorpusInputs())
        {
            var context = RuleRunSupport.CreateContext(path, sql, compatLevel);
            var lines = RuleRunSupport.SplitLines(sql);

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
                    validatedRuleIds.Add(rule.Metadata.RuleId);
                    RuleDiagnosticValidator.ValidateDiagnostic(rule.Metadata, diagnostic, lines, path, failures);

                    if (rule.Metadata.Fixable && diagnostic.Data?.Fixable is not false)
                    {
                        RuleDiagnosticValidator.ValidateFixes(rule, context, diagnostic, lines, path, failures);
                    }
                }
            }
        }

        foreach (var ruleId in SchemaDependentRuleIds.Where(ruleId => !validatedRuleIds.Contains(ruleId)))
        {
            failures.Add($"{ruleId}: schema-dependent rule emitted no diagnostic from its sample input");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} integrity violation(s):{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(80))}");
    }
}
