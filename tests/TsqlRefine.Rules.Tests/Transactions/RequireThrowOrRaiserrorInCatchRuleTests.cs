using System.Globalization;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Transactions;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Transactions;

public sealed class RequireThrowOrRaiserrorInCatchRuleTests
{
    private readonly RequireThrowOrRaiserrorInCatchRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-throw-or-raiserror-in-catch", _rule.Metadata.RuleId);
        Assert.Equal("Transactions", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    // === Positive: CATCH without error propagation ===

    [Fact]
    public void Analyze_CatchWithLogOnly_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                INSERT INTO dbo.Orders (Id) VALUES (1);
            END TRY
            BEGIN CATCH
                INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CatchWithPrintOnly_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                PRINT ERROR_MESSAGE();
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CatchWithBareReturn_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                RETURN;
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_EmptyCatch_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleTryCatchWithoutPropagation_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                PRINT 'Error 1';
            END CATCH;

            BEGIN TRY
                SELECT 2;
            END TRY
            BEGIN CATCH
                PRINT 'Error 2';
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("require-throw-or-raiserror-in-catch", d.Code));
    }

    // === Negative: CATCH with proper error propagation ===

    [Fact]
    public void Analyze_CatchWithThrow_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                INSERT INTO dbo.Orders (Id) VALUES (1);
            END TRY
            BEGIN CATCH
                INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
                THROW;
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CatchWithRaiserror_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
                RAISERROR(@msg, 16, 1);
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CatchWithReturnValue_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                INSERT INTO dbo.ErrorLog (Message) VALUES (ERROR_MESSAGE());
                RETURN -1;
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CatchWithReturnZero_ReturnsDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                RETURN 0;
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CatchWithReturnDecimalZero_InFrenchCulture_ReturnsDiagnostic()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var french = new CultureInfo("fr-FR");
            CultureInfo.CurrentCulture = french;
            CultureInfo.CurrentUICulture = french;

            const string sql = @"
                BEGIN TRY
                    SELECT 1;
                END TRY
                BEGIN CATCH
                    RETURN 0.0;
                END CATCH;
            ";
            var context = RuleTestContext.CreateContext(sql);
            var diagnostics = _rule.Analyze(context).ToArray();

            Assert.Single(diagnostics);
            Assert.Equal("require-throw-or-raiserror-in-catch", diagnostics[0].Code);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void Analyze_NestedTryCatchOuterHasThrow_NoDiagnostic()
    {
        const string sql = @"
            BEGIN TRY
                BEGIN TRY
                    SELECT 1;
                END TRY
                BEGIN CATCH
                    THROW;
                END CATCH;
            END TRY
            BEGIN CATCH
                THROW;
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("")]
    public void Analyze_NoTryCatch_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = @"
            BEGIN TRY
                SELECT 1;
            END TRY
            BEGIN CATCH
                PRINT 'Error';
            END CATCH;
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
