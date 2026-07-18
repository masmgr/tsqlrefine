using System.Collections.Frozen;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.PropertyTests;

/// <summary>
/// Property-based tests verifying that all rules produce diagnostics with
/// focused, narrow highlight ranges rather than flagging entire statements.
/// A diagnostic that spans multiple lines, or a single-line diagnostic that
/// exceeds <see cref="MaxSingleLineWidth"/> characters, is considered too wide.
/// </summary>
public sealed class DiagnosticHighlightWidthPropertyTests
{
    // ---- Thresholds ----

    /// <summary>Maximum allowed line span (End.Line - Start.Line). 0 = single-line only.</summary>
    private const int MaxLineSpan = 0;

    /// <summary>Maximum character width for single-line diagnostics.</summary>
    private const int MaxSingleLineWidth = 60;

    // ---- Rules ----

    private static readonly IReadOnlyList<IRule> AllRules = new BuiltinRuleProvider().GetRules();

    // ---- Known rules with overly wide highlights to fix later ----
    // Remove entries after their highlight ranges are narrowed.
    // KnownWideHighlightRules_StillProduceWideHighlights detects stale exemptions.
    private static readonly FrozenSet<string> KnownWideHighlightRules = FrozenSet.ToFrozenSet(
        Array.Empty<string>(),
        StringComparer.OrdinalIgnoreCase);

    // ---- Minimal SQL that triggers each rule ----

    private static readonly Dictionary<string, string> TriggerSqlByRule =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Correctness
            ["avoid-null-comparison"] = "SELECT id FROM t WHERE id = NULL;",
            ["require-parentheses-for-mixed-and-or"] = "SELECT id FROM t WHERE a = 1 OR b = 2 AND c = 3;",
            ["semantic-insert-column-count-mismatch"] = "INSERT INTO t (a, b) VALUES (1);",
            ["insert-select-column-name-mismatch"] = "INSERT INTO t (a, b) SELECT b, a FROM src;",
            ["avoid-set-rowcount"] = "SET ROWCOUNT 5;",
            ["avoid-not-in-with-null"] = "SELECT id FROM t WHERE id NOT IN (SELECT id FROM s WHERE id IS NULL);",
            ["avoid-between-for-datetime-range"] = "SELECT id FROM t WHERE created_time BETWEEN '2024-01-01' AND '2024-12-31';",
            ["aggregate-in-where-clause"] = "SELECT id FROM t WHERE COUNT(*) > 1;",
            ["avoid-max-plus-one-key-generation"] = "SET @id = (SELECT MAX(id) + 1 FROM t);",
            ["string-assignment-length-mismatch"] = "DECLARE @v varchar(1); SET @v = 'ab';",
            ["mixed-string-length-functions-in-loop"] = "WHILE DATALENGTH(@v)>0 SET @v=RIGHT(@v,LEN(@v)-1);",

            // Performance
            ["avoid-select-star"] = "SELECT * FROM t;",
            ["top-without-order-by"] = "SELECT TOP 10 id FROM t;",
            ["avoid-select-distinct"] = "SELECT DISTINCT id FROM t;",
            ["like-leading-wildcard"] = "SELECT id FROM t WHERE name LIKE '%foo';",
            ["avoid-optional-parameter-pattern"] = "SELECT id FROM t WHERE (@p IS NULL OR id = @p);",
            ["avoid-scalar-udf-in-query"] = "SELECT dbo.MyFunc(id) FROM t;",
            ["avoid-correlated-subquery-in-select"] = "SELECT (SELECT TOP 1 name FROM s WHERE s.id = t.id) FROM t;",
            ["avoid-or-on-different-columns"] = "SELECT id FROM t WHERE a = 1 OR b = 2;",

            // Safety
            ["dml-without-where"] = "DELETE FROM t;",
            ["avoid-merge"] = "MERGE t USING s ON t.id = s.id WHEN MATCHED THEN UPDATE SET t.v = s.v;",
            ["semantic-left-join-filtered-by-where"] = "SELECT t.id FROM t LEFT JOIN s ON t.id = s.id WHERE s.id = 1;",

            // Security
            ["avoid-exec-dynamic-sql"] = "EXEC(@sql);",
            ["avoid-nolock"] = "SELECT id FROM t WITH (NOLOCK);",
            ["avoid-linked-server"] = "SELECT id FROM [server].[db].[dbo].[t];",

            ["prefer-try-convert-patterns"] = "SELECT CASE WHEN ISNUMERIC(@v) = 1 THEN CONVERT(INT, @v) END;",

            // Style
            ["semicolon-termination"] = "SELECT id FROM t",
            ["require-as-for-table-alias"] = "SELECT t.id FROM t u;",
            ["require-as-for-column-alias"] = "SELECT id MyId FROM t;",
            // Note: avoid-legacy-join-syntax uses token-based detection (*= and =* operators).
            // These operators are parsed as a single token by ScriptDOM, so triggering this
            // rule via text is unreliable. The rule is tested separately in its own test class.
            // ["avoid-legacy-join-syntax"] is intentionally omitted here.
            ["require-explicit-join-type"] = "SELECT t.id FROM t JOIN s ON t.id = s.id;",
            ["normalize-inequality-operator"] = "SELECT id FROM t WHERE id != 1;",

            // Feature Usage
            ["require-column-list-for-insert-values"] = "INSERT INTO t VALUES (1, 2);",
            ["require-column-list-for-insert-select"] = "INSERT INTO t SELECT id FROM s;",
            ["avoid-full-text-search"] = "SELECT id FROM t WHERE CONTAINS(name, 'foo');",
            ["avoid-information-schema"] = "SELECT * FROM INFORMATION_SCHEMA.TABLES;",
            ["avoid-select-into"] = "SELECT id INTO #tmp FROM t;",
            ["avoid-magic-convert-style-for-datetime"] = "SELECT CONVERT(VARCHAR, GETDATE(), 101);",
            ["order-by-in-subquery"] = "SELECT id FROM (SELECT id FROM t ORDER BY id) sub;",

            // Transactions
            ["uncommitted-transaction"] = "BEGIN TRANSACTION;",

            // Debug
            ["avoid-print-statement"] = "PRINT 'debug';",
        };

    // ---- Theory data ----

    public static TheoryData<string, string> RuleData()
    {
        var data = new TheoryData<string, string>();
        foreach (var (ruleId, sql) in TriggerSqlByRule)
        {
            data.Add(ruleId, sql);
        }

        return data;
    }

    // ---- Test 1: Verify diagnostic highlight width for each rule ----

    [Theory]
    [MemberData(nameof(RuleData))]
    public void Analyze_WhenRuleReportsDiagnostic_RangeIsNarrowEnough(string ruleId, string sql)
    {
        if (KnownWideHighlightRules.Contains(ruleId))
        {
            // Skip known issues until their highlight ranges are narrowed.
            return;
        }

        var rule = AllRules.FirstOrDefault(r =>
            string.Equals(r.Metadata.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            // A missing rule ID indicates invalid test data.
            Assert.Fail($"Rule '{ruleId}' not found in BuiltinRuleProvider. Check TriggerSqlByRule entries.");
            return;
        }

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToList();

        if (diagnostics.Count == 0)
        {
            // No diagnostics means the trigger SQL needs to be updated.
            Assert.Fail(
                $"Rule '{ruleId}' produced no diagnostics for SQL: '{sql}'. " +
                $"Update TriggerSqlByRule with SQL that actually triggers this rule.");
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            var lineSpan = diagnostic.Range.End.Line - diagnostic.Range.Start.Line;
            var charWidth = diagnostic.Range.End.Character - diagnostic.Range.Start.Character;

            var isTooWide = lineSpan > MaxLineSpan || (lineSpan == 0 && charWidth > MaxSingleLineWidth);

            if (isTooWide)
            {
                Assert.Fail(
                    $"Rule '{ruleId}' has a highlight range that is too wide " +
                    $"(lineSpan={lineSpan}, charWidth={charWidth}, max={MaxSingleLineWidth}). " +
                    $"Range: [{diagnostic.Range.Start.Line}:{diagnostic.Range.Start.Character} - " +
                    $"{diagnostic.Range.End.Line}:{diagnostic.Range.End.Character}]. " +
                    $"Consider using ScriptDomHelpers.GetFirstTokenRange() or a child node instead of the full fragment.");
            }
        }
    }

    // ---- Test 2: Verify KnownWideHighlightRules IDs exist ----

    [Fact]
    public void KnownWideHighlightRules_AllExistAsBuiltinRules()
    {
        var builtinIds = AllRules
            .Select(r => r.Metadata.RuleId)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        foreach (var id in KnownWideHighlightRules)
        {
            Assert.True(
                builtinIds.Contains(id),
                $"KnownWideHighlightRules contains '{id}' which is not a built-in rule ID. " +
                $"Remove it from the exempt list.");
        }
    }

    // ---- Test 3: Verify exempt rules still produce wide highlights ----

    [Fact]
    public void KnownWideHighlightRules_StillProduceWideHighlights()
    {
        foreach (var ruleId in KnownWideHighlightRules)
        {
            var rule = AllRules.FirstOrDefault(r =>
                string.Equals(r.Metadata.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                continue; // Test 2 catches this separately.
            }

            if (!TriggerSqlByRule.TryGetValue(ruleId, out var sql) || string.IsNullOrEmpty(sql))
            {
                continue; // Skip rules without trigger SQL.
            }

            var context = RuleTestContext.CreateContext(sql);
            var diagnostics = rule.Analyze(context).ToList();

            if (diagnostics.Count == 0)
            {
                continue; // No diagnostics means width cannot be verified.
            }

            var hasWide = diagnostics.Any(d =>
            {
                var lineSpan = d.Range.End.Line - d.Range.Start.Line;
                var charWidth = d.Range.End.Character - d.Range.Start.Character;
                return lineSpan > MaxLineSpan || (lineSpan == 0 && charWidth > MaxSingleLineWidth);
            });

            Assert.True(
                hasWide,
                $"Rule '{ruleId}' is listed in KnownWideHighlightRules but its diagnostic range " +
                $"is now within the acceptable threshold (maxLineSpan={MaxLineSpan}, maxWidth={MaxSingleLineWidth}). " +
                $"Remove '{ruleId}' from KnownWideHighlightRules to enforce the narrow-highlight constraint.");
        }
    }
}
