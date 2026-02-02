using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class RequireAsForTableAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-as-for-table-alias",
        Description: "Table aliases should use the AS keyword",
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

        var visitor = new RequireAsForTableAliasVisitor(script.ScriptTokenStream);
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

        yield return RuleHelpers.CreateInsertFix("Insert AS for table alias", diagnostic.Range.Start, "AS ");
    }

    private sealed class RequireAsForTableAliasVisitor : DiagnosticVisitorBase
    {
        private readonly IList<TSqlParserToken> _tokenStream;

        public RequireAsForTableAliasVisitor(IList<TSqlParserToken> tokenStream)
        {
            _tokenStream = tokenStream;
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            // Check if the table has an alias without the AS keyword
            if (node.Alias != null && !HasAsKeyword(node.SchemaObject, node.Alias))
            {
                AddDiagnostic(
                    fragment: node.Alias,
                    message: "Table alias should use the AS keyword",
                    code: "require-as-for-table-alias",
                    category: "Style",
                    fixable: true
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            // Check if the derived table (subquery) has an alias without the AS keyword
            if (node.Alias != null && !HasAsKeyword(node.QueryExpression, node.Alias))
            {
                AddDiagnostic(
                    fragment: node.Alias,
                    message: "Table alias should use the AS keyword",
                    code: "require-as-for-table-alias",
                    category: "Style",
                    fixable: true
                );
            }

            base.ExplicitVisit(node);
        }

        private bool HasAsKeyword(TSqlFragment precedingFragment, Identifier alias)
        {
            // Look for AS keyword between the table name/subquery and the alias
            var precedingEndIndex = precedingFragment.LastTokenIndex;
            var aliasStartIndex = alias.FirstTokenIndex;

            // Check tokens between the table name and alias
            for (var i = precedingEndIndex + 1; i < aliasStartIndex; i++)
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
