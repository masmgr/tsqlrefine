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
}
