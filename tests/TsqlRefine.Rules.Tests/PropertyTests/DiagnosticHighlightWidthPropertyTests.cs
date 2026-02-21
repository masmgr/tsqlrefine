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
    // ---- 閾値 ----

    /// <summary>Maximum allowed line span (End.Line - Start.Line). 0 = single-line only.</summary>
    private const int MaxLineSpan = 0;

    /// <summary>Maximum character width for single-line diagnostics.</summary>
    private const int MaxSingleLineWidth = 60;

    // ---- ルール ----

    private static readonly IReadOnlyList<IRule> AllRules = new BuiltinRuleProvider().GetRules();

    // ---- 既知の「広すぎる」ルール（将来修正予定）----
    // 修正が完了したらここから削除すること。
    // KnownWideHighlightRules_StillProduceWideHighlights テストが
    // 修正済みなのに残留していることを検出する。
    private static readonly FrozenSet<string> KnownWideHighlightRules = FrozenSet.ToFrozenSet(
        Array.Empty<string>(),
        StringComparer.OrdinalIgnoreCase);

    // ---- 各ルールをトリガーする最小 SQL ----

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

    // ---- Theory データ ----

    public static TheoryData<string, string> RuleData()
    {
        var data = new TheoryData<string, string>();
        foreach (var (ruleId, sql) in TriggerSqlByRule)
        {
            data.Add(ruleId, sql);
        }

        return data;
    }

    // ---- テスト1: 各ルールの診断ハイライト幅を検証 ----

    [Theory]
    [MemberData(nameof(RuleData))]
    public void Analyze_WhenRuleReportsDiagnostic_RangeIsNarrowEnough(string ruleId, string sql)
    {
        if (KnownWideHighlightRules.Contains(ruleId))
        {
            // 既知の問題ルールはスキップ（将来修正予定）
            return;
        }

        var rule = AllRules.FirstOrDefault(r =>
            string.Equals(r.Metadata.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
        {
            // ルールIDが存在しない場合はテストデータ側の問題
            Assert.Fail($"Rule '{ruleId}' not found in BuiltinRuleProvider. Check TriggerSqlByRule entries.");
            return;
        }

        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToList();

        if (diagnostics.Count == 0)
        {
            // SQL がルールをトリガーしていない → テストデータを見直す必要がある
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

    // ---- テスト2: KnownWideHighlightRules の ID が実在するか確認 ----

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

    // ---- テスト3: exempt ルールが本当に広いハイライトを出しているか確認 ----

    [Fact]
    public void KnownWideHighlightRules_StillProduceWideHighlights()
    {
        foreach (var ruleId in KnownWideHighlightRules)
        {
            var rule = AllRules.FirstOrDefault(r =>
                string.Equals(r.Metadata.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                continue; // テスト2で別途検出される
            }

            if (!TriggerSqlByRule.TryGetValue(ruleId, out var sql) || string.IsNullOrEmpty(sql))
            {
                continue; // SQL 未登録はスキップ
            }

            var context = RuleTestContext.CreateContext(sql);
            var diagnostics = rule.Analyze(context).ToList();

            if (diagnostics.Count == 0)
            {
                continue; // 診断なし → 幅の検証不能
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
