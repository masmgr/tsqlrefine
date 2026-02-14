using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it.
/// </summary>
public sealed class OrderByInSubqueryRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "order-by-in-subquery";
    private const string Category = "Correctness";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new OrderByInSubqueryVisitor(context);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class OrderByInSubqueryVisitor : DiagnosticVisitorBase
    {
        private const int InvalidOrderByInSubqueryParseErrorNumber = 46047;
        private readonly HashSet<QuerySpecification> _rootQuerySpecifications = [];
        private int _selectStatementDepth;

        public OrderByInSubqueryVisitor(RuleContext context)
        {
            AddDiagnosticsFromParseErrors(context);
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            var isRootSelectStatement = _selectStatementDepth == 0;
            _selectStatementDepth++;

            if (isRootSelectStatement && node.QueryExpression is QuerySpecification querySpecification)
            {
                _rootQuerySpecifications.Add(querySpecification);
            }

            base.ExplicitVisit(node);

            _selectStatementDepth--;
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.OrderByClause == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (_rootQuerySpecifications.Contains(node))
            {
                base.ExplicitVisit(node);
                return;
            }

            if (HasValidException(node))
            {
                base.ExplicitVisit(node);
                return;
            }

            AddDiagnostic(
                fragment: node.OrderByClause,
                message: "ORDER BY in subquery is invalid unless paired with TOP, OFFSET, FOR XML, or FOR JSON.",
                code: RuleId,
                category: Category,
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        private static bool HasValidException(QuerySpecification node) =>
            node.TopRowFilter != null ||
            node.OffsetClause != null ||
            node.ForClause is XmlForClause or JsonForClause;

        private void AddDiagnosticsFromParseErrors(RuleContext context)
        {
            var rawSql = context.Ast.RawSql;

            foreach (var parseError in context.Ast.ParseErrors)
            {
                if (parseError.Number != InvalidOrderByInSubqueryParseErrorNumber)
                {
                    continue;
                }

                var startOffset = Math.Clamp(parseError.Offset, 0, rawSql.Length);
                var endOffset = Math.Clamp(startOffset + 1, 0, rawSql.Length);
                var start = TextPositionHelpers.OffsetToPosition(rawSql, startOffset);
                var end = TextPositionHelpers.OffsetToPosition(rawSql, endOffset);

                AddDiagnostic(
                    range: new PluginSdk.Range(start, end),
                    message: "ORDER BY in subquery is invalid unless paired with TOP, OFFSET, FOR XML, or FOR JSON.",
                    code: RuleId,
                    category: Category,
                    fixable: false
                );
            }
        }
    }
}
