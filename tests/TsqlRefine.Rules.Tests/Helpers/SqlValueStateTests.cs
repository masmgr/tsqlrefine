using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class SqlValueStateTests
{
    [Fact]
    public void IsUnsafeSqlText_ConstantAndNumericValue_ReturnsFalse()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM T WHERE Id = "),
            new SqlSegment(SqlTrustKind.NumericValue)
        ]);

        Assert.False(state.IsUnsafeSqlText());
    }

    [Fact]
    public void IsUnsafeSqlText_UntrustedValue_ReturnsTrue()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM T WHERE Name = '"),
            new SqlSegment(SqlTrustKind.UntrustedValue),
            new SqlSegment(SqlTrustKind.Constant, "'")
        ]);

        Assert.True(state.IsUnsafeSqlText());
    }

    [Fact]
    public void IsUnsafeSqlText_EscapedValueInsideLiteral_ReturnsFalse()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM T WHERE Name = '"),
            new SqlSegment(SqlTrustKind.EscapedStringLiteral),
            new SqlSegment(SqlTrustKind.Constant, "'")
        ]);

        Assert.False(state.IsUnsafeSqlText());
    }

    [Fact]
    public void IsUnsafeSqlText_EscapedValueOutsideLiteral_ReturnsTrue()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM "),
            new SqlSegment(SqlTrustKind.EscapedStringLiteral)
        ]);

        Assert.True(state.IsUnsafeSqlText());
    }

    [Fact]
    public void IsUnsafeSqlText_QuotedIdentifierOutsideLiteral_ReturnsFalse()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM "),
            new SqlSegment(SqlTrustKind.QuotedIdentifier)
        ]);

        Assert.False(state.IsUnsafeSqlText());
    }

    [Fact]
    public void IsUnsafeSqlText_QuotedIdentifierInsideLiteral_ReturnsTrue()
    {
        var state = new SqlValueState(SqlTrustKind.SqlFragment,
        [
            new SqlSegment(SqlTrustKind.Constant, "SELECT * FROM T WHERE Name = '"),
            new SqlSegment(SqlTrustKind.QuotedIdentifier),
            new SqlSegment(SqlTrustKind.Constant, "'")
        ]);

        Assert.True(state.IsUnsafeSqlText());
    }
}
