using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class RequireExplicitJoinTypeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-explicit-join-type",
        Description: "Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is not TSqlScript script || script.ScriptTokenStream is null)
        {
            yield break;
        }

        var visitor = new RequireExplicitJoinTypeVisitor(script.ScriptTokenStream);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        if (context.Ast.Fragment is not TSqlScript script || script.ScriptTokenStream is null)
        {
            yield break;
        }

        var collector = new FixInfoCollector(script.ScriptTokenStream, diagnostic.Range);
        context.Ast.Fragment.Accept(collector);

        if (collector.FixInfo is not null)
        {
            yield return new Fix(
                Title: collector.FixInfo.Title,
                Edits: new[] { new TextEdit(collector.FixInfo.InsertRange, collector.FixInfo.InsertText) }
            );
        }
    }

    private sealed class RequireExplicitJoinTypeVisitor : DiagnosticVisitorBase
    {
        private readonly IList<TSqlParserToken> _tokenStream;

        public RequireExplicitJoinTypeVisitor(IList<TSqlParserToken> tokenStream)
        {
            _tokenStream = tokenStream;
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            CheckJoinExplicitness(node);
            base.ExplicitVisit(node);
        }

        private void CheckJoinExplicitness(QualifiedJoin node)
        {
            var joinTokenIndex = FindJoinKeywordIndex(node);
            if (joinTokenIndex < 0)
            {
                return;
            }

            // The search boundary is after the left table reference to avoid
            // picking up keywords from nested JOINs
            var searchStartIndex = node.FirstTableReference.LastTokenIndex + 1;

            switch (node.QualifiedJoinType)
            {
                case QualifiedJoinType.Inner:
                    if (!HasInnerKeyword(joinTokenIndex, searchStartIndex))
                    {
                        ReportViolation(joinTokenIndex);
                    }
                    break;

                case QualifiedJoinType.LeftOuter:
                case QualifiedJoinType.RightOuter:
                case QualifiedJoinType.FullOuter:
                    if (!HasOuterKeyword(joinTokenIndex, searchStartIndex))
                    {
                        ReportViolation(joinTokenIndex);
                    }
                    break;
            }
        }

        private int FindJoinKeywordIndex(QualifiedJoin node)
        {
            // Search backward from the second table reference to find this node's JOIN keyword
            // This avoids finding JOIN keywords from nested joins in the first table reference
            var searchEnd = node.SecondTableReference.FirstTokenIndex;
            for (var i = searchEnd - 1; i >= node.FirstTokenIndex; i--)
            {
                if (_tokenStream[i].TokenType == TSqlTokenType.Join)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool HasOuterKeyword(int joinTokenIndex, int nodeFirstIndex)
        {
            for (var i = joinTokenIndex - 1; i >= nodeFirstIndex; i--)
            {
                var token = _tokenStream[i];

                if (IsTrivia(token))
                {
                    continue;
                }

                if (IsJoinHint(token))
                {
                    continue;
                }

                if (token.TokenType == TSqlTokenType.Outer)
                {
                    return true;
                }

                if (token.TokenType == TSqlTokenType.Left ||
                    token.TokenType == TSqlTokenType.Right ||
                    token.TokenType == TSqlTokenType.Full)
                {
                    return false;
                }

                break;
            }
            return false;
        }

        private bool HasInnerKeyword(int joinTokenIndex, int nodeFirstIndex)
        {
            for (var i = joinTokenIndex - 1; i >= nodeFirstIndex; i--)
            {
                var token = _tokenStream[i];

                if (IsTrivia(token))
                {
                    continue;
                }

                if (IsJoinHint(token))
                {
                    continue;
                }

                if (token.TokenType == TSqlTokenType.Inner)
                {
                    return true;
                }

                break;
            }
            return false;
        }

        private void ReportViolation(int joinTokenIndex)
        {
            var joinToken = _tokenStream[joinTokenIndex];
            var range = new TsqlRefine.PluginSdk.Range(
                new Position(joinToken.Line - 1, joinToken.Column - 1),
                new Position(joinToken.Line - 1, joinToken.Column - 1 + joinToken.Text.Length)
            );

            AddDiagnostic(new Diagnostic(
                Range: range,
                Message: "JOIN must be explicit: use INNER JOIN, LEFT OUTER JOIN, RIGHT OUTER JOIN, or FULL OUTER JOIN.",
                Severity: null,
                Code: "require-explicit-join-type",
                Data: new DiagnosticData("require-explicit-join-type", "Style", true)
            ));
        }

        private static bool IsTrivia(TSqlParserToken token) =>
            token.TokenType == TSqlTokenType.WhiteSpace ||
            token.TokenType == TSqlTokenType.SingleLineComment ||
            token.TokenType == TSqlTokenType.MultilineComment;

        private static bool IsJoinHint(TSqlParserToken token)
        {
            if (token.TokenType != TSqlTokenType.Identifier)
            {
                return false;
            }

            var text = token.Text;
            return string.Equals(text, "HASH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "LOOP", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "MERGE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "REMOTE", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record FixInfo(string Title, TsqlRefine.PluginSdk.Range InsertRange, string InsertText);

    private sealed class FixInfoCollector : TSqlFragmentVisitor
    {
        private readonly IList<TSqlParserToken> _tokenStream;
        private readonly TsqlRefine.PluginSdk.Range _targetRange;

        public FixInfo? FixInfo { get; private set; }

        public FixInfoCollector(IList<TSqlParserToken> tokenStream, TsqlRefine.PluginSdk.Range targetRange)
        {
            _tokenStream = tokenStream;
            _targetRange = targetRange;
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (FixInfo is not null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var joinTokenIndex = FindJoinKeywordIndex(node);
            if (joinTokenIndex < 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            var joinToken = _tokenStream[joinTokenIndex];
            var tokenStart = new Position(joinToken.Line - 1, joinToken.Column - 1);

            if (tokenStart != _targetRange.Start)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Find insert position (before JOIN or before join hint)
            var searchStartIndex = node.FirstTableReference.LastTokenIndex + 1;
            var insertIndex = FindInsertPosition(joinTokenIndex, searchStartIndex);
            var insertToken = _tokenStream[insertIndex];
            var insertPosition = new Position(insertToken.Line - 1, insertToken.Column - 1);
            var insertRange = new TsqlRefine.PluginSdk.Range(insertPosition, insertPosition);

            switch (node.QualifiedJoinType)
            {
                case QualifiedJoinType.Inner:
                    FixInfo = new FixInfo("Use INNER JOIN", insertRange, "INNER ");
                    break;

                case QualifiedJoinType.LeftOuter:
                    FixInfo = new FixInfo("Use LEFT OUTER JOIN", insertRange, "OUTER ");
                    break;

                case QualifiedJoinType.RightOuter:
                    FixInfo = new FixInfo("Use RIGHT OUTER JOIN", insertRange, "OUTER ");
                    break;

                case QualifiedJoinType.FullOuter:
                    FixInfo = new FixInfo("Use FULL OUTER JOIN", insertRange, "OUTER ");
                    break;
            }

            base.ExplicitVisit(node);
        }

        private int FindJoinKeywordIndex(QualifiedJoin node)
        {
            // Search backward from the second table reference to find this node's JOIN keyword
            var searchEnd = node.SecondTableReference.FirstTokenIndex;
            for (var i = searchEnd - 1; i >= node.FirstTokenIndex; i--)
            {
                if (_tokenStream[i].TokenType == TSqlTokenType.Join)
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindInsertPosition(int joinTokenIndex, int nodeFirstIndex)
        {
            // Start from join keyword and scan backward to find the position before join hints
            var insertIndex = joinTokenIndex;

            for (var i = joinTokenIndex - 1; i >= nodeFirstIndex; i--)
            {
                var token = _tokenStream[i];

                if (IsTrivia(token))
                {
                    continue;
                }

                if (IsJoinHint(token))
                {
                    insertIndex = i;
                    continue;
                }

                break;
            }

            return insertIndex;
        }

        private static bool IsTrivia(TSqlParserToken token) =>
            token.TokenType == TSqlTokenType.WhiteSpace ||
            token.TokenType == TSqlTokenType.SingleLineComment ||
            token.TokenType == TSqlTokenType.MultilineComment;

        private static bool IsJoinHint(TSqlParserToken token)
        {
            if (token.TokenType != TSqlTokenType.Identifier)
            {
                return false;
            }

            var text = token.Text;
            return string.Equals(text, "HASH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "LOOP", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "MERGE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "REMOTE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
