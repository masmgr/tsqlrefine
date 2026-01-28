using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireBeginEndForIfWithControlflowExceptionRuleTests
{
    private readonly RequireBeginEndForIfWithControlflowExceptionRule _rule = new();

    [Fact]
    public void Analyze_IfWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
                SET @value = 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-if-with-controlflow-exception", diagnostics[0].Code);
        Assert.Contains("BEGIN/END", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_IfWithBeginEnd_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
            BEGIN
                SET @value = 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IfWithReturn_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
                RETURN;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IfWithBreak_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            WHILE @counter < 10
            BEGIN
                IF @counter = 5
                    BREAK;
                SET @counter = @counter + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IfWithContinue_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            WHILE @counter < 10
            BEGIN
                IF @counter = 5
                    CONTINUE;
                SET @counter = @counter + 1;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IfWithThrow_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @error = 1
                THROW 50000, 'Error occurred', 1;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ElseWithoutBeginEnd_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
            BEGIN
                SET @value = 1;
            END
            ELSE
                SET @value = 0;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("require-begin-end-for-if-with-controlflow-exception", diagnostics[0].Code);
        Assert.Contains("ELSE", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ElseWithReturn_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
            BEGIN
                SET @value = 1;
            END
            ELSE
                RETURN;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ElseIf_ReturnsNoDiagnostic()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
            BEGIN
                SET @value = 1;
            END
            ELSE IF @condition = 2
            BEGIN
                SET @value = 2;
            END
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_IfElseBothWithoutBeginEnd_ReturnsTwoDiagnostics()
    {
        // Arrange
        const string sql = @"
            IF @condition = 1
                SET @value = 1;
            ELSE
                SET @value = 0;
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("require-begin-end-for-if-with-controlflow-exception", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
