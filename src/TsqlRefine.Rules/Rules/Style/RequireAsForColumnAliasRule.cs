using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class RequireAsForColumnAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-as-for-column-alias",
        Description: "Column aliases should use the AS keyword",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null || context.Ast.Fragment is not TSqlScript script)
        {
            yield break;
        }

        if (script.ScriptTokenStream is null)
        {
            yield break;
        }

        var visitor = new RequireAsForColumnAliasVisitor(script.ScriptTokenStream);
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

        yield return RuleHelpers.CreateInsertFix("Insert AS for column alias", diagnostic.Range.Start, "AS ");
    }

    private sealed class RequireAsForColumnAliasVisitor : DiagnosticVisitorBase
    {
        private readonly IList<TSqlParserToken> _tokenStream;

        public RequireAsForColumnAliasVisitor(IList<TSqlParserToken> tokenStream)
        {
            _tokenStream = tokenStream;
        }

        public override void ExplicitVisit(SelectScalarExpression node)
        {
            // Check if the column has an alias without the AS keyword
            if (node.ColumnName != null && !HasAsKeyword(node.Expression, node.ColumnName))
            {
                AddDiagnostic(
                    fragment: node.ColumnName,
                    message: "Column alias should use the AS keyword",
                    code: "require-as-for-column-alias",
                    category: "Style",
                    fixable: true
                );
            }

            base.ExplicitVisit(node);
        }

        private bool HasAsKeyword(ScalarExpression expression, IdentifierOrValueExpression columnName)
        {
            // Look for AS keyword between the expression and the alias
            var expressionEndIndex = expression.LastTokenIndex;
            var aliasStartIndex = columnName.FirstTokenIndex;

            // Check tokens between the expression and alias
            for (var i = expressionEndIndex + 1; i < aliasStartIndex; i++)
            {
                var token = _tokenStream[i];

                // Skip whitespace and comments
                if (token.TokenType == TSqlTokenType.WhiteSpace ||
                    token.TokenType == TSqlTokenType.SingleLineComment ||
                    token.TokenType == TSqlTokenType.MultilineComment)
                {
                    continue;
                }

                // Check if it's the AS keyword
                if (token.TokenType == TSqlTokenType.As)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
