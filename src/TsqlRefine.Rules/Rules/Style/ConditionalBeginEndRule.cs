using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class ConditionalBeginEndRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "conditional-begin-end",
        Description: "Require BEGIN/END blocks in conditional statements for clarity and maintainability",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new ConditionalBeginEndVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var collector = new StatementCollector(diagnostic.Range);
        context.Ast.Fragment.Accept(collector);

        if (collector.TargetStatement is null || collector.ParentIfStatement is null)
        {
            yield break;
        }

        var statement = collector.TargetStatement;
        var parentIf = collector.ParentIfStatement;
        var sql = context.Ast.RawSql;

        // Calculate the range of the statement to wrap
        var startOffset = statement.StartOffset;
        var endOffset = statement.StartOffset + statement.FragmentLength;

        // Get the indentation from the parent IF/ELSE statement
        var controlStatementOffset = collector.IsElseBranch
            ? FindElseKeywordOffset(parentIf, statement)
            : parentIf.StartOffset;
        var indent = TextPositionHelpers.GetLineIndentation(sql, controlStatementOffset);

        // Build the replacement text with BEGIN/END
        var statementText = sql.Substring(startOffset, statement.FragmentLength).TrimEnd();

        // Determine line ending style from the source
        var lineEnding = TextPositionHelpers.DetectLineEnding(sql);

        // Create the wrapped statement with proper indentation
        var newText = $"BEGIN{lineEnding}{indent}    {statementText}{lineEnding}{indent}END";

        var startPos = TextPositionHelpers.OffsetToPosition(sql, startOffset);
        var endPos = TextPositionHelpers.OffsetToPosition(sql, endOffset);

        yield return new Fix(
            Title: "Wrap with BEGIN/END block",
            Edits: [new TextEdit(new TsqlRefine.PluginSdk.Range(startPos, endPos), newText)]
        );
    }

    private static int FindElseKeywordOffset(IfStatement parentIf, TSqlStatement elseStatement)
    {
        // Use ScriptTokenStream to find the ELSE keyword between ThenStatement and ElseStatement
        var tokenStream = parentIf.ScriptTokenStream;
        if (tokenStream is null)
        {
            return elseStatement.StartOffset;
        }

        // Search tokens between ThenStatement's last token and ElseStatement's first token
        var searchStart = parentIf.ThenStatement.LastTokenIndex + 1;
        var searchEnd = elseStatement.FirstTokenIndex;

        for (var i = searchStart; i < searchEnd && i < tokenStream.Count; i++)
        {
            var token = tokenStream[i];
            if (token.TokenType == TSqlTokenType.Else)
            {
                return token.Offset;
            }
        }

        // Fallback to statement offset if ELSE not found
        return elseStatement.StartOffset;
    }

    private sealed class ConditionalBeginEndVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(IfStatement node)
        {
            // Check THEN clause
            if (BeginEndHelpers.NeedsBeginEndBlock(node.ThenStatement))
            {
                AddDiagnostic(
                    fragment: node.ThenStatement,
                    message: "IF statement should use BEGIN/END block for clarity and maintainability.",
                    code: "conditional-begin-end",
                    category: "Style",
                    fixable: true
                );
            }

            // Check ELSE clause (skip ELSE IF chains as they are intentional patterns)
            if (node.ElseStatement is not null &&
                !BeginEndHelpers.IsElseIfPattern(node.ElseStatement) &&
                BeginEndHelpers.NeedsBeginEndBlock(node.ElseStatement))
            {
                AddDiagnostic(
                    fragment: node.ElseStatement,
                    message: "ELSE statement should use BEGIN/END block for clarity and maintainability.",
                    code: "conditional-begin-end",
                    category: "Style",
                    fixable: true
                );
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed class StatementCollector(TsqlRefine.PluginSdk.Range targetRange) : TSqlFragmentVisitor
    {
        private readonly TsqlRefine.PluginSdk.Range _targetRange = targetRange;

        public TSqlStatement? TargetStatement { get; private set; }

        public IfStatement? ParentIfStatement { get; private set; }

        public bool IsElseBranch { get; private set; }

        public override void ExplicitVisit(IfStatement node)
        {
            // Check THEN clause
            if (BeginEndHelpers.NeedsBeginEndBlock(node.ThenStatement))
            {
                var range = ScriptDomHelpers.GetRange(node.ThenStatement);
                if (RangesMatch(range, _targetRange))
                {
                    TargetStatement = node.ThenStatement;
                    ParentIfStatement = node;
                    IsElseBranch = false;
                    return;
                }
            }

            // Check ELSE clause (skip ELSE IF patterns)
            if (node.ElseStatement is not null &&
                !BeginEndHelpers.IsElseIfPattern(node.ElseStatement) &&
                BeginEndHelpers.NeedsBeginEndBlock(node.ElseStatement))
            {
                var range = ScriptDomHelpers.GetRange(node.ElseStatement);
                if (RangesMatch(range, _targetRange))
                {
                    TargetStatement = node.ElseStatement;
                    ParentIfStatement = node;
                    IsElseBranch = true;
                    return;
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool RangesMatch(TsqlRefine.PluginSdk.Range a, TsqlRefine.PluginSdk.Range b)
        {
            return a.Start.Line == b.Start.Line &&
                   a.Start.Character == b.Start.Character &&
                   a.End.Line == b.End.Line &&
                   a.End.Character == b.End.Character;
        }
    }
}
