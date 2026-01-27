using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class RequireQualifiedColumnsEverywhereRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-qualified-columns-everywhere",
        Description: "Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced; stricter than qualified-select-columns.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new RequireQualifiedColumnsEverywhereVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireQualifiedColumnsEverywhereVisitor : DiagnosticVisitorBase
    {
        private bool _inMultiTableQuery;

        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.QueryExpression is not QuerySpecification querySpec || querySpec.FromClause is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Count the number of tables in FROM/JOIN
            var tableReferences = new List<TableReference>();
            CollectTableReferences(querySpec.FromClause.TableReferences, tableReferences);

            // Only check if multiple tables are present
            _inMultiTableQuery = tableReferences.Count > 1;

            base.ExplicitVisit(node);

            _inMultiTableQuery = false;
        }

        public override void ExplicitVisit(WhereClause node)
        {
            if (_inMultiTableQuery)
            {
                CheckBooleanExpressionForUnqualifiedColumns(node.SearchCondition, "WHERE");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_inMultiTableQuery && node.SearchCondition != null)
            {
                CheckBooleanExpressionForUnqualifiedColumns(node.SearchCondition, "JOIN");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OrderByClause node)
        {
            if (_inMultiTableQuery && node.OrderByElements != null)
            {
                foreach (var orderBy in node.OrderByElements)
                {
                    if (orderBy is ExpressionWithSortOrder exprWithSort)
                    {
                        CheckScalarExpressionForUnqualifiedColumns(exprWithSort.Expression, "ORDER BY");
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckBooleanExpressionForUnqualifiedColumns(BooleanExpression expression, string clause)
        {
            if (expression is BooleanComparisonExpression comparison)
            {
                CheckScalarExpressionForUnqualifiedColumns(comparison.FirstExpression, clause);
                CheckScalarExpressionForUnqualifiedColumns(comparison.SecondExpression, clause);
            }
            else if (expression is BooleanBinaryExpression binary)
            {
                CheckBooleanExpressionForUnqualifiedColumns(binary.FirstExpression, clause);
                CheckBooleanExpressionForUnqualifiedColumns(binary.SecondExpression, clause);
            }
            else if (expression is BooleanParenthesisExpression parenthesis)
            {
                CheckBooleanExpressionForUnqualifiedColumns(parenthesis.Expression, clause);
            }
        }

        private void CheckScalarExpressionForUnqualifiedColumns(ScalarExpression expression, string clause)
        {
            if (expression is ColumnReferenceExpression colRef)
            {
                // Check if column is unqualified (no table/alias prefix)
                if (colRef.MultiPartIdentifier != null && colRef.MultiPartIdentifier.Count == 1)
                {
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{colRef.MultiPartIdentifier.Identifiers[0].Value}' in {clause} clause should be qualified with table name or alias when multiple tables are present.",
                        code: "require-qualified-columns-everywhere",
                        category: "Style",
                        fixable: false
                    );
                }
            }
            else if (expression is BinaryExpression binary)
            {
                CheckScalarExpressionForUnqualifiedColumns(binary.FirstExpression, clause);
                CheckScalarExpressionForUnqualifiedColumns(binary.SecondExpression, clause);
            }
            else if (expression is FunctionCall func)
            {
                if (func.Parameters != null)
                {
                    foreach (var param in func.Parameters)
                    {
                        CheckScalarExpressionForUnqualifiedColumns(param, clause);
                    }
                }
            }
        }

        private static void CollectTableReferences(IList<TableReference> tableRefs, List<TableReference> collected)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    var leftRefs = new List<TableReference> { join.FirstTableReference };
                    CollectTableReferences(leftRefs, collected);

                    var rightRefs = new List<TableReference> { join.SecondTableReference };
                    CollectTableReferences(rightRefs, collected);
                }
                else
                {
                    collected.Add(tableRef);
                }
            }
        }
    }
}
