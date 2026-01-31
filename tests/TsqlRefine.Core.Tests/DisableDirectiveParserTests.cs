using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Tests;

public sealed class DisableDirectiveParserTests
{
    #region ParseDirectives Tests

    [Fact]
    public void ParseDirectives_EmptyTokens_ReturnsEmpty()
    {
        var tokens = Array.Empty<Token>();
        var result = DisableDirectiveParser.ParseDirectives(tokens);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseDirectives_NoComments_ReturnsEmpty()
    {
        var tokens = new Token[]
        {
            new("SELECT", new Position(0, 0), 6, "Select"),
            new(" ", new Position(0, 6), 1, "WhiteSpace"),
            new("*", new Position(0, 7), 1, "Star")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseDirectives_BlockComment_DisableAll_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-disable */", new Position(0, 0), 24, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
        Assert.Empty(result[0].RuleIds);
        Assert.Equal(0, result[0].Line);
    }

    [Fact]
    public void ParseDirectives_BlockComment_EnableAll_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-enable */", new Position(2, 0), 23, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Enable, result[0].Type);
        Assert.Empty(result[0].RuleIds);
        Assert.Equal(2, result[0].Line);
    }

    [Fact]
    public void ParseDirectives_BlockComment_DisableSingleRule_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-disable avoid-select-star */", new Position(0, 0), 42, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
        Assert.Single(result[0].RuleIds);
        Assert.Equal("avoid-select-star", result[0].RuleIds[0]);
    }

    [Fact]
    public void ParseDirectives_BlockComment_DisableMultipleRules_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-disable rule-a, rule-b, rule-c */", new Position(0, 0), 47, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
        Assert.Equal(3, result[0].RuleIds.Count);
        Assert.Equal("rule-a", result[0].RuleIds[0]);
        Assert.Equal("rule-b", result[0].RuleIds[1]);
        Assert.Equal("rule-c", result[0].RuleIds[2]);
    }

    [Fact]
    public void ParseDirectives_CaseInsensitive_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* TSQLREFINE-DISABLE */", new Position(0, 0), 24, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
    }

    [Fact]
    public void ParseDirectives_LineComment_DisableAll_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("-- tsqlrefine-disable", new Position(0, 0), 21, "SingleLineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Single(result);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
        Assert.Empty(result[0].RuleIds);
    }

    [Fact]
    public void ParseDirectives_MalformedDirective_IgnoredCorrectly()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-disabled */", new Position(0, 0), 25, "MultilineComment"), // typo
            new("/* tsqlrefine disable */", new Position(1, 0), 24, "MultilineComment"),  // missing hyphen
            new("/* some comment */", new Position(2, 0), 18, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseDirectives_WhitespaceVariations_ParsesCorrectly()
    {
        var tokens = new Token[]
        {
            new("/*tsqlrefine-disable*/", new Position(0, 0), 22, "MultilineComment"),
            new("/*  tsqlrefine-disable  */", new Position(1, 0), 26, "MultilineComment"),
            new("/* \t tsqlrefine-disable \t */", new Position(2, 0), 28, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Equal(3, result.Count);
        Assert.All(result, d => Assert.Equal(DisableDirectiveType.Disable, d.Type));
    }

    [Fact]
    public void ParseDirectives_MultipleDirectives_ParsesInOrder()
    {
        var tokens = new Token[]
        {
            new("/* tsqlrefine-disable */", new Position(0, 0), 24, "MultilineComment"),
            new("SELECT", new Position(1, 0), 6, "Select"),
            new("/* tsqlrefine-enable */", new Position(2, 0), 23, "MultilineComment")
        };
        var result = DisableDirectiveParser.ParseDirectives(tokens);

        Assert.Equal(2, result.Count);
        Assert.Equal(DisableDirectiveType.Disable, result[0].Type);
        Assert.Equal(0, result[0].Line);
        Assert.Equal(DisableDirectiveType.Enable, result[1].Type);
        Assert.Equal(2, result[1].Line);
    }

    #endregion

    #region BuildDisabledRanges Tests

    [Fact]
    public void BuildDisabledRanges_EmptyDirectives_ReturnsEmpty()
    {
        var result = DisableDirectiveParser.BuildDisabledRanges(
            Array.Empty<DisableDirective>(),
            totalLines: 10);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildDisabledRanges_SingleDisable_NoEnable_CreatesOpenRange()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), Line: 0)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Single(result);
        Assert.Null(result[0].RuleId);
        Assert.Equal(0, result[0].StartLine);
        Assert.Null(result[0].EndLine);
    }

    [Fact]
    public void BuildDisabledRanges_DisableEnable_CreatesClosedRange()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), Line: 0),
            new DisableDirective(DisableDirectiveType.Enable, Array.Empty<string>(), Line: 5)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Single(result);
        Assert.Null(result[0].RuleId);
        Assert.Equal(0, result[0].StartLine);
        Assert.Equal(5, result[0].EndLine);
    }

    [Fact]
    public void BuildDisabledRanges_RuleSpecific_DisableEnable_CreatesCorrectRange()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, new[] { "rule-a" }, Line: 1),
            new DisableDirective(DisableDirectiveType.Enable, new[] { "rule-a" }, Line: 4)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Single(result);
        Assert.Equal("rule-a", result[0].RuleId);
        Assert.Equal(1, result[0].StartLine);
        Assert.Equal(4, result[0].EndLine);
    }

    [Fact]
    public void BuildDisabledRanges_MultipleRuleIds_CreatesMultipleRanges()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, new[] { "rule-a", "rule-b" }, Line: 0),
            new DisableDirective(DisableDirectiveType.Enable, new[] { "rule-a", "rule-b" }, Line: 5)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.RuleId == "rule-a" && r.StartLine == 0 && r.EndLine == 5);
        Assert.Contains(result, r => r.RuleId == "rule-b" && r.StartLine == 0 && r.EndLine == 5);
    }

    [Fact]
    public void BuildDisabledRanges_NestedDisables_HandlesCorrectly()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), Line: 0),
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), Line: 2),
            new DisableDirective(DisableDirectiveType.Enable, Array.Empty<string>(), Line: 4),
            new DisableDirective(DisableDirectiveType.Enable, Array.Empty<string>(), Line: 6)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Equal(2, result.Count);
        // Inner range (line 2-4) closed first
        Assert.Contains(result, r => r.StartLine == 2 && r.EndLine == 4);
        // Outer range (line 0-6) closed second
        Assert.Contains(result, r => r.StartLine == 0 && r.EndLine == 6);
    }

    [Fact]
    public void BuildDisabledRanges_EnableWithoutDisable_Ignored()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Enable, Array.Empty<string>(), Line: 0)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildDisabledRanges_MixedGlobalAndRuleSpecific_HandlesCorrectly()
    {
        var directives = new[]
        {
            new DisableDirective(DisableDirectiveType.Disable, Array.Empty<string>(), Line: 0),
            new DisableDirective(DisableDirectiveType.Disable, new[] { "rule-a" }, Line: 2),
            new DisableDirective(DisableDirectiveType.Enable, new[] { "rule-a" }, Line: 4),
            new DisableDirective(DisableDirectiveType.Enable, Array.Empty<string>(), Line: 6)
        };
        var result = DisableDirectiveParser.BuildDisabledRanges(directives, totalLines: 10);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.RuleId == null && r.StartLine == 0 && r.EndLine == 6);
        Assert.Contains(result, r => r.RuleId == "rule-a" && r.StartLine == 2 && r.EndLine == 4);
    }

    #endregion

    #region IsSuppressed Tests

    [Fact]
    public void IsSuppressed_EmptyRanges_ReturnsFalse()
    {
        var diagnostic = CreateDiagnostic(line: 5, code: "rule-a");
        var result = DisableDirectiveParser.IsSuppressed(diagnostic, Array.Empty<DisabledRange>());

        Assert.False(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticInGlobalDisableRange_ReturnsTrue()
    {
        var diagnostic = CreateDiagnostic(line: 3, code: "rule-a");
        var ranges = new[] { new DisabledRange(null, StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticOutsideRange_ReturnsFalse()
    {
        var diagnostic = CreateDiagnostic(line: 7, code: "rule-a");
        var ranges = new[] { new DisabledRange(null, StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.False(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticInRuleSpecificRange_MatchingRule_ReturnsTrue()
    {
        var diagnostic = CreateDiagnostic(line: 3, code: "rule-a");
        var ranges = new[] { new DisabledRange("rule-a", StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticInRuleSpecificRange_DifferentRule_ReturnsFalse()
    {
        var diagnostic = CreateDiagnostic(line: 3, code: "rule-b");
        var ranges = new[] { new DisabledRange("rule-a", StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.False(result);
    }

    [Fact]
    public void IsSuppressed_RuleIdComparison_CaseInsensitive()
    {
        var diagnostic = CreateDiagnostic(line: 3, code: "RULE-A");
        var ranges = new[] { new DisabledRange("rule-a", StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticAtStartLine_ReturnsTrue()
    {
        var diagnostic = CreateDiagnostic(line: 0, code: "rule-a");
        var ranges = new[] { new DisabledRange(null, StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_DiagnosticAtEndLine_ReturnsFalse()
    {
        // EndLine is exclusive
        var diagnostic = CreateDiagnostic(line: 5, code: "rule-a");
        var ranges = new[] { new DisabledRange(null, StartLine: 0, EndLine: 5) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.False(result);
    }

    [Fact]
    public void IsSuppressed_OpenEndedRange_ReturnsTrue()
    {
        var diagnostic = CreateDiagnostic(line: 100, code: "rule-a");
        var ranges = new[] { new DisabledRange(null, StartLine: 0, EndLine: null) };

        var result = DisableDirectiveParser.IsSuppressed(diagnostic, ranges);

        Assert.True(result);
    }

    #endregion

    #region CountLines Tests

    [Fact]
    public void CountLines_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, DisableDirectiveParser.CountLines(""));
    }

    [Fact]
    public void CountLines_SingleLine_ReturnsOne()
    {
        Assert.Equal(1, DisableDirectiveParser.CountLines("SELECT 1"));
    }

    [Fact]
    public void CountLines_MultipleLines_LF_ReturnsCorrect()
    {
        Assert.Equal(3, DisableDirectiveParser.CountLines("line1\nline2\nline3"));
    }

    [Fact]
    public void CountLines_MultipleLines_CRLF_ReturnsCorrect()
    {
        Assert.Equal(3, DisableDirectiveParser.CountLines("line1\r\nline2\r\nline3"));
    }

    [Fact]
    public void CountLines_MultipleLines_CR_ReturnsCorrect()
    {
        Assert.Equal(3, DisableDirectiveParser.CountLines("line1\rline2\rline3"));
    }

    #endregion

    #region Helpers

    private static Diagnostic CreateDiagnostic(int line, string code)
    {
        return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(
                new Position(line, 0),
                new Position(line, 10)),
            Message: "Test diagnostic",
            Code: code
        );
    }

    #endregion
}
