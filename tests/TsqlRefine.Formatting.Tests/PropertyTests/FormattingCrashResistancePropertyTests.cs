using FsCheck.Fluent;
using Xunit;

namespace TsqlRefine.Formatting.Tests.PropertyTests;

/// <summary>
/// Property-based tests verifying the formatter handles arbitrary SQL input
/// without throwing exceptions (crash resistance / fuzzing).
/// </summary>
public sealed class FormattingCrashResistancePropertyTests
{
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

    private static readonly string[] DdlFragments =
    [
        "CREATE", "ALTER", "DROP", "TABLE", "VIEW", "INDEX", "PROCEDURE", "FUNCTION",
        "TRIGGER", "SCHEMA", "DATABASE", "SEQUENCE", "TYPE", "SYNONYM",
        "CLUSTERED", "NONCLUSTERED", "UNIQUE", "PRIMARY", "KEY", "FOREIGN",
        "REFERENCES", "CONSTRAINT", "DEFAULT", "CHECK", "IDENTITY",
        "INT", "VARCHAR(50)", "NVARCHAR(MAX)", "DATETIME", "BIT", "DECIMAL(18,2)",
        "NOT", "NULL", "ON", "GO", ";", "(", ")", ",",
        "dbo", ".", "MyTable", "MyColumn", "MyIndex", "MyProc",
        "AS", "BEGIN", "END", "RETURN", "SET", "NOCOUNT",
    ];

    [Fact]
    public void Format_NeverThrowsOnRandomFragments()
    {
        var gen = Gen.Choose(1, 15).SelectMany(
            count => Gen.Elements(SqlFragments).ListOf(count),
            (_, fragments) => string.Join(" ", fragments));

        var samples = gen.Sample(200, 200);

        foreach (var sql in samples)
        {
            try
            {
                _ = SqlFormatter.Format(sql);
            }
#pragma warning disable CA1031 // Intentional: testing crash resistance
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Assert.Fail(
                    $"Format threw {ex.GetType().Name} on input '{Truncate(sql, 80)}': {ex.Message}");
            }
        }
    }

    [Fact]
    public void Format_NeverThrowsOnStructuredSql()
    {
        var gen = Gen.Elements("*", "Id", "Name", "1", "GETDATE()")
            .SelectMany(
                _ => Gen.Elements("t", "dbo.Users", "[My Table]", "#temp"),
                (col, tbl) => $"SELECT {col} FROM {tbl};");

        var samples = gen.Sample(100, 100);

        foreach (var sql in samples)
        {
            try
            {
                _ = SqlFormatter.Format(sql);
            }
#pragma warning disable CA1031 // Intentional: testing crash resistance
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Assert.Fail(
                    $"Format threw {ex.GetType().Name} on input '{sql}': {ex.Message}");
            }
        }
    }

    [Fact]
    public void Format_NeverThrowsOnEdgeCases()
    {
        string[] edgeCases =
        [
            "",
            " ",
            "\t",
            "\n",
            "\r\n",
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
            "SELECT /* unclosed block comment",
            "'",
            "/*",
            "[",
            "SELECT 1\r\nGO\r\nSELECT 2",
            "SELECT ''escaped'' FROM t",
            "SELECT [bracket]]ed] FROM t",
        ];

        foreach (var sql in edgeCases)
        {
            try
            {
                _ = SqlFormatter.Format(sql);
            }
#pragma warning disable CA1031 // Intentional: testing crash resistance
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Assert.Fail(
                    $"Format threw {ex.GetType().Name} on input '{Truncate(sql, 80)}': {ex.Message}");
            }
        }
    }

    [Fact]
    public void Format_NeverThrowsWithRandomOptions()
    {
        // Generate SQL + options tuples together to avoid index alignment issues
        var gen = Gen.Choose(1, 15).SelectMany(
            count => Gen.Elements(SqlFragments).ListOf(count),
            (_, fragments) => string.Join(" ", fragments));

        var sqlSamples = gen.Sample(200, 200);

        int[] sizes = [2, 4, 8];
        ElementCasing[] casings = [ElementCasing.Upper, ElementCasing.Lower, ElementCasing.None];
        var rng = new Random(42);

        foreach (var sql in sqlSamples)
        {
            var opts = new FormattingOptions
            {
                IndentStyle = rng.Next(2) == 0 ? IndentStyle.Spaces : IndentStyle.Tabs,
                IndentSize = sizes[rng.Next(sizes.Length)],
                KeywordElementCasing = casings[rng.Next(casings.Length)],
                CommaStyle = rng.Next(2) == 0 ? CommaStyle.Leading : CommaStyle.Trailing,
                LineEnding = rng.Next(2) == 0 ? LineEnding.Lf : LineEnding.CrLf,
                InsertFinalNewline = rng.Next(2) == 1,
                TrimTrailingWhitespace = rng.Next(2) == 1,
                NormalizeInlineSpacing = rng.Next(2) == 1,
                NormalizeOperatorSpacing = rng.Next(2) == 1,
                NormalizeKeywordSpacing = rng.Next(2) == 1,
            };

            try
            {
                _ = SqlFormatter.Format(sql, opts);
            }
#pragma warning disable CA1031 // Intentional: testing crash resistance
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Assert.Fail(
                    $"Format threw {ex.GetType().Name} on input '{Truncate(sql, 80)}' with options: {ex.Message}");
            }
        }
    }

    [Fact]
    public void Format_NeverThrowsOnDdlStatements()
    {
        var gen = Gen.Choose(1, 15).SelectMany(
            count => Gen.Elements(DdlFragments).ListOf(count),
            (_, fragments) => string.Join(" ", fragments));

        var samples = gen.Sample(100, 100);

        foreach (var sql in samples)
        {
            try
            {
                _ = SqlFormatter.Format(sql);
            }
#pragma warning disable CA1031 // Intentional: testing crash resistance
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Assert.Fail(
                    $"Format threw {ex.GetType().Name} on input '{Truncate(sql, 80)}': {ex.Message}");
            }
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
