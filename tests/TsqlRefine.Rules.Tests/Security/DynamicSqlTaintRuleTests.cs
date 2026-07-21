using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class DynamicSqlTaintRuleTests
{
    private readonly DynamicSqlTaintRule _rule = new();

    [Fact]
    public void Metadata_HasExpectedValues()
    {
        Assert.Equal("dynamic-sql-taint", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_ProcedureParameterFlowsThroughVariable_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.FindUser @userName nvarchar(100)
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = ''';
                SET @sql = @sql + @userName + N'''';
                EXEC sys.sp_executesql @sql;
            END;
            """;

        var diagnostic = Assert.Single(Analyze(sql));

        Assert.Equal("dynamic-sql-taint", diagnostic.Code);
        Assert.Contains("untrusted", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_TaintFlowsAcrossAssignments_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RunSearch @input nvarchar(100)
            AS
            BEGIN
                DECLARE @fragment nvarchar(max) = @input;
                DECLARE @copy nvarchar(max);
                SET @copy = @fragment;
                EXEC(@copy);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_SelectColumnFlowsIntoSql_ReturnsDiagnostic()
    {
        const string sql = """
            DECLARE @sql nvarchar(max);
            SELECT @sql = N'SELECT * FROM ' + TableName FROM dbo.QueryQueue;
            EXEC(@sql);
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_DirectProcedureParameterExec_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.RunSql @sql nvarchar(max) AS EXEC(@sql);";

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_DirectProcedureParameterSpExecutesql_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.RunSql @sql nvarchar(max) AS EXEC sys.sp_executesql @sql;";

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_ParameterizedSpExecutesql_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.FindUser @userName nvarchar(100)
            AS
            EXEC sys.sp_executesql
                N'SELECT * FROM dbo.Users WHERE Name = @name',
                N'@name nvarchar(100)',
                @name = @userName;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_ConstantVariableSql_ReturnsEmpty()
    {
        const string sql = "DECLARE @sql nvarchar(max) = N'SELECT 1'; EXEC(@sql);";

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_QuotenameAtIdentifierPosition_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.ReadTable @tableName sysname
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM ' + QUOTENAME(@tableName);
                EXEC(@sql);
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_QuotenameInsideStringLiteral_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.FindUser @name nvarchar(100)
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = ''' + QUOTENAME(@name) + N'''';
                EXEC(@sql);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_ReplaceEscapingInsideStringLiteral_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.FindUser @name nvarchar(100)
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Name = '''
                    + REPLACE(@name, N'''', N'''''') + N'''';
                EXEC(@sql);
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_ReplaceEscapingAtIdentifierPosition_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.ReadTable @tableName sysname
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM ' + REPLACE(@tableName, N'''', N'''''');
                EXEC(@sql);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_ExplicitNumericConversion_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.FindUser @id nvarchar(100)
            AS
            BEGIN
                DECLARE @sql nvarchar(max) = N'SELECT * FROM dbo.Users WHERE Id = '
                    + CONVERT(int, @id);
                EXEC(@sql);
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_UnsafeValueOnOneBranch_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RunSql @input nvarchar(max), @useInput bit
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                IF @useInput = 1
                    SET @sql = @input;
                ELSE
                    SET @sql = N'SELECT 1';
                EXEC(@sql);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_DifferentConstantSqlOnBranches_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RunReport @detailed bit
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                IF @detailed = 1
                    SET @sql = N'SELECT Id, Name FROM dbo.Users';
                ELSE
                    SET @sql = N'SELECT Id FROM dbo.Users';
                EXEC sys.sp_executesql @sql;
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_SearchedCaseWithConstantResultsAndQuotedIdentifier_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.ChangeTable @name sysname, @alter bit
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                SET @sql = CASE WHEN @alter = 1 THEN N'ALTER' ELSE N'CREATE' END
                    + N' TABLE ' + QUOTENAME(@name);
                EXEC sys.sp_executesql @sql;
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_SimpleCaseWithConstantResultsAndQuotedIdentifier_ReturnsEmpty()
    {
        const string sql = """
            CREATE PROCEDURE dbo.ReadTable @name sysname, @kind int
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                SET @sql = CASE @kind WHEN 1 THEN N'SELECT * FROM ' ELSE N'SELECT TOP (1) * FROM ' END
                    + QUOTENAME(@name);
                EXEC sys.sp_executesql @sql;
            END;
            """;

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_CaseWithUnsafeResult_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RunSql @input nvarchar(max), @useInput bit
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                SET @sql = CASE WHEN @useInput = 1 THEN @input ELSE N'SELECT 1' END;
                EXEC sys.sp_executesql @sql;
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Theory]
    [InlineData("EXEC dbo.BuildQuery @sql OUTPUT;")]
    [InlineData("EXEC @result = dbo.BuildQuery;")]
    public void Analyze_ExecWriteInvalidatesConstant_ReturnsDiagnostic(string overwrite)
    {
        var variable = overwrite.Contains("@result", StringComparison.Ordinal) ? "@result" : "@sql";
        var sql = $"DECLARE {variable} nvarchar(max) = N'SELECT 1'; {overwrite} EXEC({variable});";

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_FetchIntoInvalidatesConstant_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @sql nvarchar(max) = N'SELECT 1'; " +
            "DECLARE query_cursor CURSOR FOR SELECT SqlText FROM dbo.Queue; " +
            "OPEN query_cursor; FETCH NEXT FROM query_cursor INTO @sql; EXEC(@sql);";

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_DifferentOpenLiteralConstantsBeforeQuotename_ReturnsDiagnostic()
    {
        const string sql = """
            CREATE PROCEDURE dbo.RunSql @name sysname, @flag bit
            AS
            BEGIN
                DECLARE @sql nvarchar(max);
                IF @flag = 1
                    SET @sql = N'SELECT * FROM dbo.A WHERE Name = ''';
                ELSE
                    SET @sql = N'SELECT * FROM dbo.B WHERE Name = ''';
                SET @sql = @sql + QUOTENAME(@name) + N'''';
                EXEC(@sql);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void Analyze_GotoScope_ReturnsEmpty()
    {
        const string sql = "CREATE PROCEDURE dbo.RunSql @sql nvarchar(max) AS BEGIN GOTO done; done: EXEC(@sql); END;";

        Assert.Empty(Analyze(sql));
    }

    [Fact]
    public void Analyze_TriggerBody_IsAnalyzed()
    {
        const string sql = """
            CREATE TRIGGER dbo.RunDynamicSql ON dbo.Target AFTER INSERT AS
            BEGIN
                DECLARE @sql nvarchar(max);
                SELECT @sql = SqlText FROM inserted;
                EXEC(@sql);
            END;
            """;

        Assert.Single(Analyze(sql));
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        const string sql = "CREATE PROCEDURE dbo.RunSql @sql nvarchar(max) AS EXEC(@sql);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Empty(_rule.GetFixes(context, diagnostic));
    }

    private IReadOnlyList<Diagnostic> Analyze(string sql) =>
        _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();
}
