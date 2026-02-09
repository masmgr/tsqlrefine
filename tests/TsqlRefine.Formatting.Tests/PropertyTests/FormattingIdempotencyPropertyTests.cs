using FsCheck.Xunit;

namespace TsqlRefine.Formatting.Tests.PropertyTests;

/// <summary>
/// Property-based tests verifying formatting idempotency:
/// format(format(x)) == format(x) for any valid SQL and any formatting options.
/// </summary>
public sealed class FormattingIdempotencyPropertyTests
{
    private static readonly string[] SqlTemplates =
    [
        "SELECT {0} FROM {1}",
        "SELECT {0}, {1} FROM dbo.Users WHERE Id = 1",
        "INSERT INTO {1} ({0}) VALUES (1)",
        "UPDATE {1} SET {0} = 1 WHERE Id = 1",
        "DELETE FROM {1} WHERE {0} = 1",
        "SELECT {0} FROM {1} INNER JOIN dbo.Orders ON {1}.Id = dbo.Orders.UserId",
        "SELECT TOP 10 {0} FROM {1} ORDER BY {0}",
        "SELECT {0}, COUNT(*) FROM {1} GROUP BY {0} HAVING COUNT(*) > 1",
        "SELECT CASE WHEN {0} IS NULL THEN 0 ELSE {0} END FROM {1}",
        "SELECT {0} FROM {1} WHERE {0} IN (SELECT {0} FROM {1})",
        "WITH cte AS (SELECT {0} FROM {1}) SELECT * FROM cte",
        "SELECT ISNULL({0}, 0), COALESCE({0}, 0) FROM {1}",
        "SELECT {0} + 1, {0} - 1, {0} * 2, {0} / 2 FROM {1}",
        "SELECT {0} FROM {1} WHERE {0} BETWEEN 1 AND 10",
        "SELECT CAST({0} AS INT), CONVERT(VARCHAR(50), {0}) FROM {1}",
    ];

    private static readonly string[] ColumnNames = ["Id", "Name", "Email", "CreatedAt", "IsActive", "Amount"];
    private static readonly string[] TableNames = ["dbo.Users", "dbo.Orders", "dbo.Products", "Sales.Customers"];

    private static string GenerateSql(int templateIdx, int colIdx, int tblIdx)
    {
        var t = ((templateIdx % SqlTemplates.Length) + SqlTemplates.Length) % SqlTemplates.Length;
        var c = ((colIdx % ColumnNames.Length) + ColumnNames.Length) % ColumnNames.Length;
        var tb = ((tblIdx % TableNames.Length) + TableNames.Length) % TableNames.Length;
        return string.Format(SqlTemplates[t], ColumnNames[c], TableNames[tb]);
    }

    private static FormattingOptions GenerateOptions(
        bool useSpaces, int sizeIdx, int casingIdx, bool leading, bool useLf,
        bool insertFinal, bool trim, bool normInline, bool normOp, bool normKeyword)
    {
        int[] sizes = [2, 4, 8];
        ElementCasing[] casings = [ElementCasing.Upper, ElementCasing.Lower, ElementCasing.None];

        return new FormattingOptions
        {
            IndentStyle = useSpaces ? IndentStyle.Spaces : IndentStyle.Tabs,
            IndentSize = sizes[((sizeIdx % sizes.Length) + sizes.Length) % sizes.Length],
            KeywordElementCasing = casings[((casingIdx % casings.Length) + casings.Length) % casings.Length],
            CommaStyle = leading ? CommaStyle.Leading : CommaStyle.Trailing,
            LineEnding = useLf ? LineEnding.Lf : LineEnding.CrLf,
            InsertFinalNewline = insertFinal,
            TrimTrailingWhitespace = trim,
            NormalizeInlineSpacing = normInline,
            NormalizeOperatorSpacing = normOp,
            NormalizeKeywordSpacing = normKeyword,
        };
    }

    [Property(MaxTest = 200)]
    public bool Format_IsIdempotent(
        int templateIdx, int colIdx, int tblIdx,
        bool useSpaces, int sizeIdx, int casingIdx, bool leading, bool useLf,
        bool insertFinal, bool trim, bool normInline, bool normOp, bool normKeyword)
    {
        var sql = GenerateSql(templateIdx, colIdx, tblIdx);
        var opts = GenerateOptions(useSpaces, sizeIdx, casingIdx, leading, useLf, insertFinal, trim, normInline, normOp, normKeyword);

        var once = SqlFormatter.Format(sql, opts);
        var twice = SqlFormatter.Format(once, opts);

        return once == twice;
    }

    [Property(MaxTest = 200)]
    public bool Format_PreservesNonEmptyOutput(int templateIdx, int colIdx, int tblIdx)
    {
        var sql = GenerateSql(templateIdx, colIdx, tblIdx);
        var result = SqlFormatter.Format(sql, new FormattingOptions());

        return !string.IsNullOrEmpty(result);
    }

    [Property(MaxTest = 100)]
    public bool Format_EmptyInput_IsIdempotent(
        bool useSpaces, int sizeIdx, int casingIdx, bool leading, bool useLf,
        bool insertFinal, bool trim, bool normInline, bool normOp, bool normKeyword)
    {
        var opts = GenerateOptions(useSpaces, sizeIdx, casingIdx, leading, useLf, insertFinal, trim, normInline, normOp, normKeyword);

        var once = SqlFormatter.Format("", opts);
        var twice = SqlFormatter.Format(once, opts);

        return once == twice;
    }

    [Property(MaxTest = 200)]
    public bool Format_CommentsArePreserved(int templateIdx, int colIdx, int tblIdx)
    {
        var sql = GenerateSql(templateIdx, colIdx, tblIdx) + " -- inline comment";
        var result = SqlFormatter.Format(sql, new FormattingOptions());

        return result.Contains("--");
    }
}
