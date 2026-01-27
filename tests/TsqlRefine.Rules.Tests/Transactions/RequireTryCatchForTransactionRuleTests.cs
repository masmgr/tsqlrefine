using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;
using TsqlRefine.Rules.Rules.Transactions;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class RequireTryCatchForTransactionRuleTests
{
    private readonly RequireTryCatchForTransactionRule _rule = new();

    [Theory]
    [InlineData("BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;")]
    [InlineData("BEGIN TRAN; INSERT INTO logs (message) VALUES ('test'); COMMIT;")]
    [InlineData("BEGIN TRANSACTION txn; DELETE FROM orders; COMMIT TRANSACTION txn;")]
    [InlineData("begin transaction; update users set status = 'active'; commit;")]  // lowercase
    public void Analyze_WhenTransactionWithoutTryCatch_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-try-catch-for-transaction", diagnostics[0].Code);
        Assert.Contains("TRY/CATCH", diagnostics[0].Message);
    }

    [Theory]
    [InlineData(@"
        BEGIN TRY
            BEGIN TRANSACTION;
            UPDATE users SET active = 1;
            COMMIT;
        END TRY
        BEGIN CATCH
            ROLLBACK;
            THROW;
        END CATCH;")]
    [InlineData(@"
        BEGIN TRY
            BEGIN TRAN;
            INSERT INTO logs (message) VALUES ('test');
            COMMIT TRAN;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
        END CATCH;")]
    [InlineData("SELECT * FROM users;")]  // No transaction
    [InlineData("UPDATE users SET active = 1;")]  // No explicit transaction
    [InlineData("")]  // Empty
    public void Analyze_WhenValid_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NestedTryCatch_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            BEGIN TRY
                BEGIN TRY
                    BEGIN TRANSACTION;
                    UPDATE users SET active = 1;
                    COMMIT;
                END TRY
                BEGIN CATCH
                    ROLLBACK;
                END CATCH;
            END TRY
            BEGIN CATCH
                THROW;
            END CATCH;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleTransactionsInTry_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            BEGIN TRY
                BEGIN TRANSACTION;
                UPDATE users SET active = 1;
                COMMIT;

                BEGIN TRANSACTION;
                INSERT INTO logs (message) VALUES ('test');
                COMMIT;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK;
            END CATCH;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TransactionOutsideAndInsideTryCatch_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            BEGIN TRANSACTION;
            UPDATE users SET active = 1;
            COMMIT;

            BEGIN TRY
                BEGIN TRANSACTION;
                INSERT INTO logs (message) VALUES ('test');
                COMMIT;
            END TRY
            BEGIN CATCH
                ROLLBACK;
            END CATCH;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-try-catch-for-transaction", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_TransactionInProcedureWithoutTryCatch_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            CREATE PROCEDURE UpdateUser
            AS
            BEGIN
                BEGIN TRANSACTION;
                UPDATE users SET active = 1;
                COMMIT;
            END;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.NotEmpty(diagnostics);
        Assert.Equal("require-try-catch-for-transaction", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("require-try-catch-for-transaction", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("BEGIN TRANSACTION; UPDATE users SET active = 1; COMMIT;");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "require-try-catch-for-transaction"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql)
    {
        var parser = new TSql150Parser(initialQuotedIdentifiers: true);

        using var fragmentReader = new StringReader(sql);
        var fragment = parser.Parse(fragmentReader, out IList<ParseError> parseErrors);

        using var tokenReader = new StringReader(sql);
        var tokenStream = parser.GetTokenStream(tokenReader, out IList<ParseError> tokenErrors);

        var tokens = tokenStream
            .Where(token => token.TokenType != TSqlTokenType.EndOfFile)
            .Select(token =>
            {
                var text = token.Text ?? string.Empty;
                return new Token(
                    text,
                    new Position(Math.Max(0, token.Line - 1), Math.Max(0, token.Column - 1)),
                    text.Length,
                    token.TokenType.ToString());
            })
            .ToArray();

        var ast = new ScriptDomAst(sql, fragment, parseErrors.ToArray(), tokenErrors.ToArray());

        return new RuleContext(
            FilePath: "<test>",
            CompatLevel: 150,
            Ast: ast,
            Tokens: tokens,
            Settings: new RuleSettings()
        );
    }
}
