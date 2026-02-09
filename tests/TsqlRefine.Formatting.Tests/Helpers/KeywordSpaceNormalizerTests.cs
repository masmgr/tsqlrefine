using TsqlRefine.Formatting.Helpers.Whitespace;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class KeywordSpaceNormalizerTests
{
    private readonly FormattingOptions _defaultOptions = new();

    // --- Basic edge cases ---

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        var result = KeywordSpaceNormalizer.Normalize("", _defaultOptions);
        Assert.Equal("", result);
    }

    [Fact]
    public void Normalize_NullString_ReturnsNull()
    {
        var result = KeywordSpaceNormalizer.Normalize(null!, _defaultOptions);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_DisabledOption_ReturnsUnchanged()
    {
        var options = new FormattingOptions { NormalizeKeywordSpacing = false };
        var input = "SELECT id FROM users LEFT   OUTER   JOIN orders o ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, options);
        Assert.Equal(input, result);
    }

    // --- JOIN variants ---

    [Fact]
    public void Normalize_LeftOuterJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 LEFT   OUTER   JOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 LEFT OUTER JOIN t2 ON 1 = 1", result);
    }

    [Fact]
    public void Normalize_RightOuterJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 RIGHT   OUTER   JOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 RIGHT OUTER JOIN t2 ON 1 = 1", result);
    }

    [Fact]
    public void Normalize_FullOuterJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 FULL   OUTER   JOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 FULL OUTER JOIN t2 ON 1 = 1", result);
    }

    [Fact]
    public void Normalize_InnerJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 INNER   JOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 INNER JOIN t2 ON 1 = 1", result);
    }

    [Fact]
    public void Normalize_CrossJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 CROSS   JOIN t2";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 CROSS JOIN t2", result);
    }

    [Fact]
    public void Normalize_LeftJoin_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 LEFT   JOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 LEFT JOIN t2 ON 1 = 1", result);
    }

    [Fact]
    public void Normalize_CrossApply_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 CROSS   APPLY fn(t1.id)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 CROSS APPLY fn(t1.id)", result);
    }

    [Fact]
    public void Normalize_OuterApply_CollapsesSpaces()
    {
        var input = "SELECT 1 FROM t1 OUTER   APPLY fn(t1.id)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 OUTER APPLY fn(t1.id)", result);
    }

    // --- BY clauses ---

    [Fact]
    public void Normalize_GroupBy_CollapsesSpaces()
    {
        var input = "SELECT id, COUNT(*) FROM t GROUP   BY id";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id, COUNT(*) FROM t GROUP BY id", result);
    }

    [Fact]
    public void Normalize_OrderBy_CollapsesSpaces()
    {
        var input = "SELECT id FROM t ORDER   BY id";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT id FROM t ORDER BY id", result);
    }

    [Fact]
    public void Normalize_PartitionBy_CollapsesSpaces()
    {
        var input = "SELECT ROW_NUMBER() OVER (PARTITION   BY id ORDER BY name) FROM t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT ROW_NUMBER() OVER (PARTITION BY id ORDER BY name) FROM t", result);
    }

    // --- Negation ---

    [Fact]
    public void Normalize_IsNotNull_CollapsesSpaces()
    {
        var input = "SELECT 1 WHERE col IS   NOT   NULL";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE col IS NOT NULL", result);
    }

    [Fact]
    public void Normalize_NotIn_CollapsesSpaces()
    {
        var input = "SELECT 1 WHERE id NOT   IN (1, 2, 3)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE id NOT IN (1, 2, 3)", result);
    }

    [Fact]
    public void Normalize_NotExists_CollapsesSpaces()
    {
        var input = "SELECT 1 WHERE NOT   EXISTS (SELECT 1 FROM t)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM t)", result);
    }

    [Fact]
    public void Normalize_NotBetween_CollapsesSpaces()
    {
        var input = "SELECT 1 WHERE id NOT   BETWEEN 1 AND 10";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE id NOT BETWEEN 1 AND 10", result);
    }

    [Fact]
    public void Normalize_NotLike_CollapsesSpaces()
    {
        var input = "SELECT 1 WHERE name NOT   LIKE '%test%'";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 WHERE name NOT LIKE '%test%'", result);
    }

    // --- DML ---

    [Fact]
    public void Normalize_InsertInto_CollapsesSpaces()
    {
        var input = "INSERT   INTO t (id) VALUES (1)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("INSERT INTO t (id) VALUES (1)", result);
    }

    [Fact]
    public void Normalize_DeleteFrom_CollapsesSpaces()
    {
        var input = "DELETE   FROM t WHERE id = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("DELETE FROM t WHERE id = 1", result);
    }

    // --- Set operations ---

    [Fact]
    public void Normalize_UnionAll_CollapsesSpaces()
    {
        var input = "SELECT 1 UNION   ALL SELECT 2";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 UNION ALL SELECT 2", result);
    }

    // --- DDL ---

    [Fact]
    public void Normalize_CreateTable_CollapsesSpaces()
    {
        var input = "CREATE   TABLE t (id int)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("CREATE TABLE t (id int)", result);
    }

    [Fact]
    public void Normalize_AlterTable_CollapsesSpaces()
    {
        var input = "ALTER   TABLE t ADD col int";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("ALTER TABLE t ADD col int", result);
    }

    [Fact]
    public void Normalize_DropTable_CollapsesSpaces()
    {
        var input = "DROP   TABLE t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("DROP TABLE t", result);
    }

    // --- Constraints ---

    [Fact]
    public void Normalize_PrimaryKey_CollapsesSpaces()
    {
        var input = "CREATE TABLE t (id int PRIMARY   KEY)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("CREATE TABLE t (id int PRIMARY KEY)", result);
    }

    [Fact]
    public void Normalize_ForeignKey_CollapsesSpaces()
    {
        var input = "ALTER TABLE t ADD CONSTRAINT fk FOREIGN   KEY (id) REFERENCES t2(id)";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("ALTER TABLE t ADD CONSTRAINT fk FOREIGN KEY (id) REFERENCES t2(id)", result);
    }

    // --- Transaction ---

    [Fact]
    public void Normalize_BeginTransaction_CollapsesSpaces()
    {
        var input = "BEGIN   TRANSACTION";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("BEGIN TRANSACTION", result);
    }

    [Fact]
    public void Normalize_BeginTran_CollapsesSpaces()
    {
        var input = "BEGIN   TRAN";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("BEGIN TRAN", result);
    }

    // --- Control flow ---

    [Fact]
    public void Normalize_BeginTry_CollapsesSpaces()
    {
        var input = "BEGIN   TRY SELECT 1 END   TRY BEGIN   CATCH SELECT 0 END   CATCH";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("BEGIN TRY SELECT 1 END TRY BEGIN CATCH SELECT 0 END CATCH", result);
    }

    // --- Preservation: already single space ---

    [Fact]
    public void Normalize_SingleSpace_PreservesUnchanged()
    {
        var input = "SELECT 1 FROM t1 LEFT OUTER JOIN t2 ON 1 = 1 GROUP BY id ORDER BY name";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(input, result);
    }

    // --- Preservation: keyword + identifier spacing ---

    [Fact]
    public void Normalize_SelectAndIdentifier_PreservesSpaces()
    {
        var input = "SELECT  id FROM  users";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT  id FROM  users", result);
    }

    [Fact]
    public void Normalize_KeywordAndIdentifier_PreservesSpaces()
    {
        var input = "SELECT 1 FROM  users  WHERE  id = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM  users  WHERE  id = 1", result);
    }

    [Fact]
    public void Normalize_AlignedColumns_PreservesAlignment()
    {
        var input = "SELECT  col1,\r\n        col2\r\nFROM t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal(input, result);
    }

    // --- Protected regions ---

    [Fact]
    public void Normalize_InsideString_PreservesContent()
    {
        var input = "SELECT 'LEFT   OUTER   JOIN' FROM t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 'LEFT   OUTER   JOIN' FROM t", result);
    }

    [Fact]
    public void Normalize_InsideBlockComment_PreservesContent()
    {
        var input = "SELECT 1 /* LEFT   OUTER   JOIN */ FROM t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 /* LEFT   OUTER   JOIN */ FROM t", result);
    }

    [Fact]
    public void Normalize_InsideLineComment_PreservesContent()
    {
        var input = "SELECT 1 -- LEFT   OUTER   JOIN";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 -- LEFT   OUTER   JOIN", result);
    }

    [Fact]
    public void Normalize_InsideBracketedIdentifier_PreservesContent()
    {
        var input = "SELECT [LEFT   OUTER   JOIN] FROM t";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT [LEFT   OUTER   JOIN] FROM t", result);
    }

    // --- Multi-line ---

    [Fact]
    public void Normalize_MultilineQuery_NormalizesEachLine()
    {
        var input = "SELECT 1\r\nFROM t1\r\nLEFT   OUTER   JOIN t2 ON 1 = 1\r\nGROUP   BY id";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1\r\nFROM t1\r\nLEFT OUTER JOIN t2 ON 1 = 1\r\nGROUP BY id", result);
    }

    [Fact]
    public void Normalize_PreservesLineBreaks()
    {
        // Line break between keywords should NOT be collapsed
        var input = "LEFT\r\nOUTER\r\nJOIN t ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        // Whitespace tokens containing line breaks should be preserved as-is
        Assert.Equal(input, result);
    }

    // --- Case insensitivity ---

    [Fact]
    public void Normalize_CaseInsensitive_WorksWithMixedCase()
    {
        var input = "SELECT 1 FROM t1 left   outer   join t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 left outer join t2 ON 1 = 1", result);
    }

    // --- Tab between keywords ---

    [Fact]
    public void Normalize_TabsBetweenKeywords_CollapsesToSingleSpace()
    {
        var input = "SELECT 1 FROM t1 LEFT\tOUTER\tJOIN t2 ON 1 = 1";
        var result = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        Assert.Equal("SELECT 1 FROM t1 LEFT OUTER JOIN t2 ON 1 = 1", result);
    }

    // --- Idempotency ---

    [Fact]
    public void Normalize_AlreadyNormalized_IsIdempotent()
    {
        var input = "SELECT 1 FROM t1 LEFT OUTER JOIN t2 ON 1 = 1 GROUP BY id ORDER BY name";
        var first = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        var second = KeywordSpaceNormalizer.Normalize(first, _defaultOptions);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Normalize_WithExtraSpaces_IsIdempotent()
    {
        var input = "SELECT 1 FROM t1 LEFT   OUTER   JOIN t2 ON 1 = 1 GROUP   BY id";
        var first = KeywordSpaceNormalizer.Normalize(input, _defaultOptions);
        var second = KeywordSpaceNormalizer.Normalize(first, _defaultOptions);
        Assert.Equal(first, second);
    }
}
