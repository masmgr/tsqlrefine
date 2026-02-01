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
            ? FindElseKeywordOffset(sql, parentIf, statement)
            : parentIf.StartOffset;
        var indent = GetIndentation(sql, controlStatementOffset);

        // Build the replacement text with BEGIN/END
        var statementText = sql.Substring(startOffset, statement.FragmentLength).TrimEnd();

        // Determine line ending style from the source
        var lineEnding = DetectLineEnding(sql);

        // Create the wrapped statement with proper indentation
        var newText = $"BEGIN{lineEnding}{indent}    {statementText}{lineEnding}{indent}END";

        var startPos = OffsetToPosition(sql, startOffset);
        var endPos = OffsetToPosition(sql, endOffset);

        yield return new Fix(
            Title: "Wrap with BEGIN/END block",
            Edits: [new TextEdit(new TsqlRefine.PluginSdk.Range(startPos, endPos), newText)]
        );
    }

    private static int FindElseKeywordOffset(string sql, IfStatement parentIf, TSqlStatement elseStatement)
    {
        // Search backwards from the ELSE statement to find the ELSE keyword
        var searchStart = parentIf.ThenStatement.StartOffset + parentIf.ThenStatement.FragmentLength;
        var searchEnd = elseStatement.StartOffset;

        for (var i = searchStart; i < searchEnd; i++)
        {
            if (i + 4 <= sql.Length &&
                char.ToUpperInvariant(sql[i]) == 'E' &&
                char.ToUpperInvariant(sql[i + 1]) == 'L' &&
                char.ToUpperInvariant(sql[i + 2]) == 'S' &&
                char.ToUpperInvariant(sql[i + 3]) == 'E')
            {
                // Verify it's not part of a larger identifier
                var beforeValid = i == 0 || !char.IsLetterOrDigit(sql[i - 1]);
                var afterValid = i + 4 >= sql.Length || !char.IsLetterOrDigit(sql[i + 4]);
                if (beforeValid && afterValid)
                {
                    return i;
                }
            }
        }

        // Fallback to statement offset if ELSE not found
        return elseStatement.StartOffset;
    }

    private static string GetIndentation(string sql, int offset)
    {
        // Find the start of the line containing this offset
        var lineStart = offset;
        while (lineStart > 0 && sql[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        // Extract leading whitespace
        var indent = new System.Text.StringBuilder();
        for (var i = lineStart; i < offset && i < sql.Length; i++)
        {
            var ch = sql[i];
            if (ch == ' ' || ch == '\t')
            {
                indent.Append(ch);
            }
            else
            {
                break;
            }
        }

        return indent.ToString();
    }

    private static string DetectLineEnding(string sql)
    {
        var crlfIndex = sql.IndexOf("\r\n", StringComparison.Ordinal);
        var lfIndex = sql.IndexOf('\n');

        if (crlfIndex >= 0 && (lfIndex < 0 || crlfIndex <= lfIndex))
        {
            return "\r\n";
        }

        return "\n";
    }

    private static Position OffsetToPosition(string sql, int offset)
    {
        var line = 0;
        var character = 0;

        for (var i = 0; i < offset && i < sql.Length; i++)
        {
            if (sql[i] == '\r')
            {
                if (i + 1 < sql.Length && sql[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
            }
            else if (sql[i] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return new Position(line, character);
    }

    private sealed class ConditionalBeginEndVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(IfStatement node)
        {
            // Check THEN clause
            if (node.ThenStatement is not null && node.ThenStatement is not BeginEndBlockStatement)
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
            if (node.ElseStatement is not null && node.ElseStatement is not BeginEndBlockStatement && node.ElseStatement is not IfStatement)
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
            if (node.ThenStatement is not null && node.ThenStatement is not BeginEndBlockStatement)
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

            // Check ELSE clause
            if (node.ElseStatement is not null && node.ElseStatement is not BeginEndBlockStatement && node.ElseStatement is not IfStatement)
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
