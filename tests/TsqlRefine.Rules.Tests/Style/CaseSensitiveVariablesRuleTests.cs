using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class CaseSensitiveVariablesRuleTests
{
    private readonly CaseSensitiveVariablesRule _rule = new();

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    [Fact]
    public void Analyze_VariableWithConsistentCasing_NoDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @UserName NVARCHAR(50);
SET @UserName = 'John';
SELECT @UserName;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_VariableWithInconsistentCasing_ReturnsDiagnostic()
    {
        // Arrange
        var sql = @"
DECLARE @UserName NVARCHAR(50);
SET @username = 'John';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/case-sensitive-variables", diagnostic.Code);
        Assert.Contains("@username", diagnostic.Message);
        Assert.Contains("@UserName", diagnostic.Message);
    }

    [Fact]
    public void Analyze_MultipleVariablesWithMixedCasing_ReturnsMultipleDiagnostics()
    {
        // Arrange
        var sql = @"
DECLARE @UserName NVARCHAR(50);
DECLARE @UserId INT;
SET @username = 'John';
SET @USERID = 123;
SELECT @USERNAME, @userId;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(4, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("semantic/case-sensitive-variables", d.Code));
    }

    [Fact]
    public void Analyze_VariableInSelectAssignment_ChecksCasing()
    {
        // Arrange
        var sql = @"
DECLARE @UserName NVARCHAR(50);
SELECT @username = Name FROM Users;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/case-sensitive-variables", diagnostic.Code);
    }

    [Fact]
    public void Analyze_VariableInWhereClause_ChecksCasing()
    {
        // Arrange
        var sql = @"
DECLARE @MinValue INT = 10;
SELECT * FROM Users WHERE Age > @minvalue;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/case-sensitive-variables", diagnostic.Code);
    }

    [Fact]
    public void Analyze_MultipleDeclarationsInSameStatement_TracksAllVariables()
    {
        // Arrange
        var sql = @"
DECLARE @FirstName NVARCHAR(50), @LastName NVARCHAR(50);
SET @firstname = 'John';
SET @LASTNAME = 'Doe';";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_VariableInExpression_ChecksCasing()
    {
        // Arrange
        var sql = @"
DECLARE @Price DECIMAL(10, 2) = 100.00;
DECLARE @Tax DECIMAL(10, 2) = 0.08;
SELECT @price * (1 + @TAX) AS Total;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_NestedScopes_TracksVariablesSeparately()
    {
        // Arrange
        var sql = @"
DECLARE @Outer INT = 1;
BEGIN
    DECLARE @Inner INT = 2;
    SELECT @outer, @INNER;
END;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_VariableInStoredProcedure_ChecksCasing()
    {
        // Arrange
        var sql = @"
CREATE PROCEDURE dbo.GetUser
    @UserId INT
AS
BEGIN
    SELECT * FROM Users WHERE Id = @userid;
END;";

        // Act
        var diagnostics = _rule.Analyze(CreateContext(sql)).ToArray();

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("semantic/case-sensitive-variables", diagnostic.Code);
    }
}
