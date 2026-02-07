using FsCheck.Fluent;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.PropertyTests;

/// <summary>
/// Property-based tests verifying all rules handle arbitrary SQL input
/// without throwing exceptions (crash resistance / fuzzing).
/// </summary>
public sealed class RuleCrashResistancePropertyTests
{
    private static readonly IReadOnlyList<IRule> AllRules = new BuiltinRuleProvider().GetRules();

    private static readonly string[] SqlFragments =
    [
        "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "JOIN",
        "ON", "AND", "OR", "NOT", "NULL", "IS", "IN", "EXISTS",
        "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "PROCEDURE",
        "FUNCTION", "TRIGGER", "BEGIN", "END", "IF", "ELSE", "WHILE",
        "DECLARE", "SET", "EXEC", "EXECUTE", "PRINT", "RETURN",
        "TRY", "CATCH", "THROW", "RAISERROR", "TRANSACTION", "COMMIT", "ROLLBACK",
        "MERGE", "USING", "WHEN", "MATCHED", "THEN",
        "CASE", "CAST", "CONVERT", "COALESCE", "ISNULL",
        "TOP", "ORDER", "BY", "GROUP", "HAVING", "UNION", "ALL",
        "WITH", "AS", "NOLOCK", "HOLDLOCK", "ROWLOCK",
        "*", ",", ";", "(", ")", "=", "<>", ">=", "<=", "+", "-",
        "'string'", "123", "@var", "@@ROWCOUNT", "dbo.Table1",
        "[Quoted Identifier]", "-- comment", "/* block */",
    ];

    [Fact]
    public void AllRules_NeverThrowOnRandomFragments()
    {
        var gen = Gen.Choose(1, 15).SelectMany(
            count => Gen.Elements(SqlFragments).ListOf(count),
            (_, fragments) => string.Join(" ", fragments));

        var samples = gen.Sample(200, 200);

        foreach (var sql in samples)
        {
            var context = RuleTestContext.CreateContext(sql);

            foreach (var rule in AllRules)
            {
                try
                {
                    _ = rule.Analyze(context).ToList();
                }
#pragma warning disable CA1031 // Intentional: testing crash resistance
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    Assert.Fail(
                        $"Rule {rule.Metadata.RuleId} threw {ex.GetType().Name} on input '{Truncate(sql, 80)}': {ex.Message}");
                }
            }
        }
    }

    [Fact]
    public void AllRules_NeverThrowOnStructuredSql()
    {
        var gen = Gen.Elements("*", "Id", "Name", "1", "GETDATE()")
            .SelectMany(
                _ => Gen.Elements("t", "dbo.Users", "[My Table]", "#temp"),
                (col, tbl) => $"SELECT {col} FROM {tbl};");

        var samples = gen.Sample(100, 100);

        foreach (var sql in samples)
        {
            var context = RuleTestContext.CreateContext(sql);

            foreach (var rule in AllRules)
            {
                try
                {
                    _ = rule.Analyze(context).ToList();
                }
#pragma warning disable CA1031 // Intentional: testing crash resistance
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    Assert.Fail(
                        $"Rule {rule.Metadata.RuleId} threw {ex.GetType().Name} on input '{sql}': {ex.Message}");
                }
            }
        }
    }

    [Fact]
    public void AllRules_NeverThrowOnEdgeCases()
    {
        string[] edgeCases =
        [
            "",
            " ",
            "\t",
            "\n",
            ";",
            ";;",
            "()",
            "SELECT",
            "SELECT;",
            "SELECT *",
            "-- just a comment",
            "/* block comment */",
            "SELECT 'unclosed string",
            "SELECT [unclosed bracket",
        ];

        foreach (var sql in edgeCases)
        {
            var context = RuleTestContext.CreateContext(sql);

            foreach (var rule in AllRules)
            {
                try
                {
                    _ = rule.Analyze(context).ToList();
                }
#pragma warning disable CA1031 // Intentional: testing crash resistance
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    Assert.Fail(
                        $"Rule {rule.Metadata.RuleId} threw {ex.GetType().Name} on input '{sql}': {ex.Message}");
                }
            }
        }
    }

    [Theory]
    [InlineData("SELECT * FROM t;")]
    [InlineData("INSERT INTO t (x) VALUES (1);")]
    [InlineData("UPDATE t SET x = 1;")]
    [InlineData("DELETE FROM t;")]
    [InlineData("EXEC sp_executesql @sql;")]
    [InlineData("SELECT 1 WHERE 1 = NULL;")]
    [InlineData("DECLARE @x INT; SET @x = 1;")]
    public void AllRules_ReturnNonNullDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);

        foreach (var rule in AllRules)
        {
            var diagnostics = rule.Analyze(context).ToList();
            foreach (var diag in diagnostics)
            {
                Assert.NotNull(diag);
                Assert.NotNull(diag.Range);
                Assert.False(string.IsNullOrEmpty(diag.Message),
                    $"Rule {rule.Metadata.RuleId} returned diagnostic with empty Message");
            }
        }
    }

    [Fact]
    public void AllRules_HaveValidMetadata()
    {
        foreach (var rule in AllRules)
        {
            var meta = rule.Metadata;
            Assert.False(string.IsNullOrEmpty(meta.RuleId), $"Rule has empty RuleId: {rule.GetType().Name}");
            Assert.False(string.IsNullOrEmpty(meta.Description), $"Rule {meta.RuleId} has empty Description");
            Assert.False(string.IsNullOrEmpty(meta.Category), $"Rule {meta.RuleId} has empty Category");
            Assert.False(meta.RuleId.Any(char.IsUpper), $"Rule {meta.RuleId} has uppercase characters (should be kebab-case)");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
