using System.Text;
using TsqlRefine.Formatting.Helpers;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class ProtectedRegionTrackerTests
{
    #region IsInProtectedRegion - Initial State

    [Fact]
    public void IsInProtectedRegion_InitialState_ReturnsFalse()
    {
        var tracker = new ProtectedRegionTracker();
        Assert.False(tracker.IsInProtectedRegion());
    }

    #endregion

    #region TryStartProtectedRegion - String Literals

    [Fact]
    public void TryStartProtectedRegion_SingleQuote_StartsStringRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'test";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal(1, index);
        Assert.Equal("'", output.ToString());
    }

    [Fact]
    public void TryStartProtectedRegion_NotSingleQuote_ReturnsFalse()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "test";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.False(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal(0, index);
        Assert.Equal("", output.ToString());
    }

    #endregion

    #region TryStartProtectedRegion - Double Quote Identifiers

    [Fact]
    public void TryStartProtectedRegion_DoubleQuote_StartsDoubleQuoteRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "\"test";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal(1, index);
        Assert.Equal("\"", output.ToString());
    }

    #endregion

    #region TryStartProtectedRegion - Bracket Identifiers

    [Fact]
    public void TryStartProtectedRegion_OpenBracket_StartsBracketRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "[test";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal(1, index);
        Assert.Equal("[", output.ToString());
    }

    #endregion

    #region TryStartProtectedRegion - Block Comments

    [Fact]
    public void TryStartProtectedRegion_BlockCommentStart_StartsBlockCommentRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/* comment";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal(2, index);
        Assert.Equal("/*", output.ToString());
    }

    [Fact]
    public void TryStartProtectedRegion_SlashOnly_DoesNotStartComment()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/ test";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.False(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal(0, index);
    }

    [Fact]
    public void TryStartProtectedRegion_SlashAtEnd_DoesNotStartComment()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/";
        var index = 0;

        var result = tracker.TryStartProtectedRegion(text, output, ref index);

        Assert.False(result);
        Assert.False(tracker.IsInProtectedRegion());
    }

    #endregion

    #region TryConsume - String Literals

    [Fact]
    public void TryConsume_StringRegion_ConsumesCharacter()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'test'";
        var index = 0;

        // Start string region
        tracker.TryStartProtectedRegion(text, output, ref index);
        output.Clear();

        // Consume 't'
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.Equal("t", output.ToString());
        Assert.Equal(2, index);
    }

    [Fact]
    public void TryConsume_StringRegion_ClosingQuote_ExitsRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'t'";
        var index = 0;

        // Start string region
        tracker.TryStartProtectedRegion(text, output, ref index);
        // Consume 't'
        tracker.TryConsume(text, output, ref index);
        output.Clear();

        // Consume closing quote
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("'", output.ToString());
    }

    [Fact]
    public void TryConsume_StringRegion_EscapedQuote_StaysInRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'''";
        var index = 0;

        // Start string region
        tracker.TryStartProtectedRegion(text, output, ref index);
        output.Clear();

        // Consume escaped quote ''
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal("''", output.ToString());
        Assert.Equal(3, index);
    }

    #endregion

    #region TryConsume - Double Quote Identifiers

    [Fact]
    public void TryConsume_DoubleQuoteRegion_ClosingQuote_ExitsRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "\"t\"";
        var index = 0;

        // Start double quote region
        tracker.TryStartProtectedRegion(text, output, ref index);
        // Consume 't'
        tracker.TryConsume(text, output, ref index);
        output.Clear();

        // Consume closing quote
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("\"", output.ToString());
    }

    #endregion

    #region TryConsume - Bracket Identifiers

    [Fact]
    public void TryConsume_BracketRegion_ClosingBracket_ExitsRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "[t]";
        var index = 0;

        // Start bracket region
        tracker.TryStartProtectedRegion(text, output, ref index);
        // Consume 't'
        tracker.TryConsume(text, output, ref index);
        output.Clear();

        // Consume closing bracket
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("]", output.ToString());
    }

    [Fact]
    public void TryConsume_BracketRegion_EscapedBracket_StaysInRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "[]]";
        var index = 0;

        // Start bracket region
        tracker.TryStartProtectedRegion(text, output, ref index);
        output.Clear();

        // Consume escaped bracket ]]
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal("]]", output.ToString());
        Assert.Equal(3, index);
    }

    #endregion

    #region TryConsume - Block Comments

    [Fact]
    public void TryConsume_BlockCommentRegion_ConsumesCharacter()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/* x */";
        var index = 0;

        // Start block comment
        tracker.TryStartProtectedRegion(text, output, ref index);
        output.Clear();

        // Consume ' '
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal(" ", output.ToString());
    }

    [Fact]
    public void TryConsume_BlockCommentRegion_ClosingSequence_ExitsRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/**/";
        var index = 0;

        // Start block comment
        tracker.TryStartProtectedRegion(text, output, ref index);
        output.Clear();

        // Consume closing */
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("*/", output.ToString());
        Assert.Equal(4, index);
    }

    [Fact]
    public void TryConsume_BlockCommentRegion_AsteriskOnly_StaysInRegion()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/* * test */";
        var index = 0;

        // Start block comment
        tracker.TryStartProtectedRegion(text, output, ref index);
        // Consume space
        tracker.TryConsume(text, output, ref index);
        output.Clear();

        // Consume asterisk (not followed by /)
        var result = tracker.TryConsume(text, output, ref index);

        Assert.True(result);
        Assert.True(tracker.IsInProtectedRegion());
        Assert.Equal("*", output.ToString());
    }

    #endregion

    #region TryConsume - Not In Protected Region

    [Fact]
    public void TryConsume_NotInProtectedRegion_ReturnsFalse()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "test";
        var index = 0;

        var result = tracker.TryConsume(text, output, ref index);

        Assert.False(result);
        Assert.Equal(0, index);
        Assert.Equal("", output.ToString());
    }

    #endregion

    #region TryStartLineComment

    [Fact]
    public void TryStartLineComment_DoubleDash_StartsLineComment()
    {
        var output = new StringBuilder();
        var text = "-- comment";
        var index = 0;
        var inLineComment = false;

        var result = ProtectedRegionTracker.TryStartLineComment(text, output, ref index, ref inLineComment);

        Assert.True(result);
        Assert.True(inLineComment);
        Assert.Equal(text.Length, index);
        Assert.Equal("-- comment", output.ToString());
    }

    [Fact]
    public void TryStartLineComment_SingleDash_DoesNotStartComment()
    {
        var output = new StringBuilder();
        var text = "- test";
        var index = 0;
        var inLineComment = false;

        var result = ProtectedRegionTracker.TryStartLineComment(text, output, ref index, ref inLineComment);

        Assert.False(result);
        Assert.False(inLineComment);
        Assert.Equal(0, index);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void TryStartLineComment_DashAtEnd_DoesNotStartComment()
    {
        var output = new StringBuilder();
        var text = "-";
        var index = 0;
        var inLineComment = false;

        var result = ProtectedRegionTracker.TryStartLineComment(text, output, ref index, ref inLineComment);

        Assert.False(result);
        Assert.False(inLineComment);
    }

    [Fact]
    public void TryStartLineComment_NotDash_DoesNotStartComment()
    {
        var output = new StringBuilder();
        var text = "SELECT";
        var index = 0;
        var inLineComment = false;

        var result = ProtectedRegionTracker.TryStartLineComment(text, output, ref index, ref inLineComment);

        Assert.False(result);
        Assert.False(inLineComment);
    }

    [Fact]
    public void TryStartLineComment_ConsumesEntireLine()
    {
        var output = new StringBuilder();
        var text = "-- this is a comment with special chars !@#$%";
        var index = 0;
        var inLineComment = false;

        ProtectedRegionTracker.TryStartLineComment(text, output, ref index, ref inLineComment);

        Assert.Equal(text.Length, index);
        Assert.Equal(text, output.ToString());
    }

    #endregion

    #region Complete Workflows

    [Fact]
    public void Workflow_StringLiteral_StartsAndEnds()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'hello'";
        var index = 0;

        // Start
        tracker.TryStartProtectedRegion(text, output, ref index);
        Assert.True(tracker.IsInProtectedRegion());

        // Consume content
        while (index < text.Length && tracker.IsInProtectedRegion())
        {
            tracker.TryConsume(text, output, ref index);
        }

        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("'hello'", output.ToString());
    }

    [Fact]
    public void Workflow_BracketIdentifier_WithEscapedBracket()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "[Col]]Name]";
        var index = 0;

        // Start
        tracker.TryStartProtectedRegion(text, output, ref index);

        // Consume all content
        while (index < text.Length && tracker.IsInProtectedRegion())
        {
            tracker.TryConsume(text, output, ref index);
        }

        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("[Col]]Name]", output.ToString());
    }

    [Fact]
    public void Workflow_BlockComment_MultiLineSimulated()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "/* start */";
        var index = 0;

        // Start
        tracker.TryStartProtectedRegion(text, output, ref index);

        // Consume until end
        while (index < text.Length && tracker.IsInProtectedRegion())
        {
            tracker.TryConsume(text, output, ref index);
        }

        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("/* start */", output.ToString());
    }

    [Fact]
    public void Workflow_NestedQuoteInString_HandlesCorrectly()
    {
        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var text = "'it''s ok'";
        var index = 0;

        // Start
        tracker.TryStartProtectedRegion(text, output, ref index);

        // Consume all
        while (index < text.Length && tracker.IsInProtectedRegion())
        {
            tracker.TryConsume(text, output, ref index);
        }

        Assert.False(tracker.IsInProtectedRegion());
        Assert.Equal("'it''s ok'", output.ToString());
    }

    #endregion
}
