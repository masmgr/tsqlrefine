using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class ConditionalBeginEndRuleTests
{
    [Fact]
    public void Metadata_ShouldHaveCorrectProperties()
    {
        var rule = new ConditionalBeginEndRule();

        Assert.Equal("conditional-begin-end", rule.Metadata.RuleId);
        Assert.Equal("Style", rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Information, rule.Metadata.DefaultSeverity);
        Assert.True(rule.Metadata.Fixable);
    }

    [Theory]
    [InlineData("IF @x = 1 SELECT 1")]
    [InlineData("IF @x > 0 UPDATE users SET active = 1")]
    [InlineData(@"
        IF @x = 1
            SELECT 1
        ELSE
            SELECT 2")]
    [InlineData(@"
        IF @x > 0
            PRINT 'positive'")]
    public void Analyze_WhenIfWithoutBeginEnd_ReturnsDiagnostic(string sql)
    {
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("conditional-begin-end", d.Code));
        Assert.All(diagnostics, d => Assert.Contains("BEGIN", d.Message));
    }

    [Theory]
    [InlineData(@"
        IF @x = 1
        BEGIN
            SELECT 1
        END")]
    [InlineData(@"
        IF @x = 1
        BEGIN
            SELECT 1
        END
        ELSE
        BEGIN
            SELECT 2
        END")]
    [InlineData(@"
        IF @x > 0
        BEGIN
            UPDATE users SET active = 1
        END")]
    public void Analyze_WhenIfWithBeginEnd_ReturnsNoDiagnostic(string sql)
    {
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_WhenElseWithoutBeginEnd_ReturnsDiagnostic()
    {
        var sql = @"
            IF @x = 1
            BEGIN
                SELECT 1
            END
            ELSE
                SELECT 2";

        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("conditional-begin-end", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_WhenMultipleIfWithoutBeginEnd_ReturnsMultipleDiagnostics()
    {
        var sql = @"
            IF @x = 1 SELECT 1
            IF @y = 2 SELECT 2
        ";

        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("conditional-begin-end", d.Code));
    }

    [Fact]
    public void Analyze_WhenElseIfChain_OnlyReportsNonIfBranches()
    {
        var sql = @"
            IF @x = 1
                SELECT 1
            ELSE IF @x = 2
                SELECT 2
            ELSE
                SELECT 3";

        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostics = rule.Analyze(context).ToArray();

        // Should report: outer IF then, inner IF then, and final ELSE
        // ELSE IF itself is not reported as it's an intentional pattern
        Assert.Equal(3, diagnostics.Length);
    }

    [Fact]
    public void GetFixes_WhenIfWithoutBeginEnd_ReturnsFixWithBeginEnd()
    {
        var sql = "IF @x = 1 SELECT 1";
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Single(fixes);
        Assert.Equal("Wrap with BEGIN/END block", fixes[0].Title);
        Assert.Single(fixes[0].Edits);

        var updated = Apply(sql, fixes[0].Edits.First());
        Assert.Contains("BEGIN", updated);
        Assert.Contains("END", updated);
        Assert.Contains("SELECT 1", updated);
    }

    [Fact]
    public void GetFixes_WhenMultilineIfWithoutBeginEnd_UsesIfIndentation()
    {
        var sql = "    IF @x = 1\n        SELECT 1";
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Single(fixes);
        var updated = Apply(sql, fixes[0].Edits.First());

        // BEGIN/END should use IF's indentation (4 spaces), not the statement's (8 spaces)
        Assert.Contains("    BEGIN", updated);
        Assert.Contains("        SELECT 1", updated);  // Statement gets IF indent + 4 spaces
        Assert.Contains("    END", updated);
    }

    [Fact]
    public void GetFixes_WhenElseWithoutBeginEnd_UsesElseIndentation()
    {
        var sql = "    IF @x = 1\n    BEGIN\n        SELECT 1\n    END\n    ELSE\n        SELECT 2";
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Single(fixes);
        var updated = Apply(sql, fixes[0].Edits.First());

        // BEGIN/END for ELSE branch should use ELSE's indentation (4 spaces)
        // Count occurrences to verify both IF's BEGIN/END and ELSE's BEGIN/END
        var beginCount = updated.Split(["BEGIN"], StringSplitOptions.None).Length - 1;
        Assert.Equal(2, beginCount);  // One for IF, one for ELSE fix
        Assert.Contains("SELECT 2", updated);
    }

    [Fact]
    public void GetFixes_WhenComplexStatement_WrapsCorrectly()
    {
        var sql = "IF @x = 1 UPDATE users SET active = 1 WHERE id = @id";
        var rule = new ConditionalBeginEndRule();
        var context = CreateContext(sql);
        var diagnostic = rule.Analyze(context).First();
        var fixes = rule.GetFixes(context, diagnostic).ToArray();

        Assert.Single(fixes);
        var updated = Apply(sql, fixes[0].Edits.First());
        Assert.Contains("BEGIN", updated);
        Assert.Contains("UPDATE users SET active = 1 WHERE id = @id", updated);
        Assert.Contains("END", updated);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }

    private static string Apply(string text, TextEdit edit)
    {
        var startIndex = IndexFromPosition(text, edit.Range.Start);
        var endIndex = IndexFromPosition(text, edit.Range.End);
        return string.Concat(text.AsSpan(0, startIndex), edit.NewText, text.AsSpan(endIndex));
    }

    private static int IndexFromPosition(string text, Position position)
    {
        var line = 0;
        var character = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (line == position.Line && character == position.Character)
            {
                return i;
            }

            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        if (line == position.Line && character == position.Character)
        {
            return text.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(position), $"Position {position.Line}:{position.Character} is outside the text.");
    }
}
