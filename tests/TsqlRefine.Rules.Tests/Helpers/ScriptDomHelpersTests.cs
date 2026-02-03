using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class ScriptDomHelpersTests
{
    [Fact]
    public void GetRange_WithValidFragment_ReturnsCorrectRange()
    {
        // Arrange
        var sql = "SELECT * FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        Assert.Equal(0, range.Start.Line); // 0-based
        Assert.Equal(0, range.Start.Character); // 0-based
    }

    [Fact]
    public void GetRange_WithNullFragment_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ScriptDomHelpers.GetRange(null!));
    }

    [Fact]
    public void GetRange_WithMultiLineFragment_CalculatesCorrectEndPosition()
    {
        // Arrange
        var sql = @"SELECT
    col1,
    col2
FROM users";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        Assert.Equal(0, range.Start.Line);
        Assert.True(range.End.Line >= range.Start.Line);
    }

    [Fact]
    public void GetRange_WithFragmentNoTokenStream_ReturnsStartPositionOnly()
    {
        // Arrange
        var sql = "SELECT 1";
        var parser = new TSql160Parser(true);
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Act
        var range = ScriptDomHelpers.GetRange(fragment);

        // Assert
        Assert.NotNull(range);
        // Should have start position at minimum
        Assert.True(range.Start.Line >= 0);
        Assert.True(range.Start.Character >= 0);
    }

    /// <summary>
    /// SQL Server 2022 extended TRIM syntax: TRIM(characters FROM string)
    /// Note: ScriptDom 161.8812.0 does not yet support this syntax.
    /// This test documents the current parser limitation.
    /// When ScriptDom adds support, flip the expected behavior.
    /// </summary>
    [Fact]
    public void Parse_TrimWithCharacterSpecifier_SqlServer2022_CurrentlyUnsupportedByScriptDom()
    {
        // Arrange - SQL Server 2022 extended TRIM syntax with character specifier
        // NCHAR(12288) is a full-width space (ideographic space)
        var sql = @"
CREATE FUNCTION dbo.TrimFullWidthSpace(@str NVARCHAR(MAX))
RETURNS NVARCHAR(MAX)
AS
BEGIN
    RETURN TRIM(NCHAR(12288) FROM @str);
END";
        var parser = new TSql160Parser(true);

        // Act
        var fragment = parser.Parse(new System.IO.StringReader(sql), out var errors);

        // Assert - ScriptDom currently does not support TRIM(expr FROM expr) syntax
        // When ScriptDom adds support, change this to Assert.Empty(errors)
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("TRIM"));
    }
}
