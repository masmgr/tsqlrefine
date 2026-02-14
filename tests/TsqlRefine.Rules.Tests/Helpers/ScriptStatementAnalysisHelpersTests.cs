using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ScriptStatementAnalysisHelpersTests
{
    [Fact]
    public void ShouldEnforcePreambleChecks_WithCreateOrAlterProcedure_ReturnsTrue()
    {
        // Arrange
        var script = ParseScript("CREATE OR ALTER PROCEDURE dbo.Test AS BEGIN SELECT 1; END;");

        // Act
        var result = ScriptStatementAnalysisHelpers.ShouldEnforcePreambleChecks(script);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldEnforcePreambleChecks_WithCreateOrAlterFunction_ReturnsTrue()
    {
        // Arrange
        var script = ParseScript("""
            CREATE OR ALTER FUNCTION dbo.GetOne()
            RETURNS INT
            AS
            BEGIN
                RETURN 1;
            END;
            """);

        // Act
        var result = ScriptStatementAnalysisHelpers.ShouldEnforcePreambleChecks(script);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldEnforcePreambleChecks_WithShortScriptWithoutCreate_ReturnsFalse()
    {
        // Arrange
        var script = ParseScript("SELECT 1;");

        // Act
        var result = ScriptStatementAnalysisHelpers.ShouldEnforcePreambleChecks(script);

        // Assert
        Assert.False(result);
    }

    private static TSqlScript ParseScript(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        var fragment = parser.Parse(new StringReader(sql), out var errors);
        Assert.Empty(errors);
        return (TSqlScript)fragment;
    }
}
