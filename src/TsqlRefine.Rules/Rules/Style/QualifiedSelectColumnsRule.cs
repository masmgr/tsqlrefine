using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class QualifiedSelectColumnsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "qualified-select-columns",
        Description: "Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap.",
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

        var visitor = new QualifiedSelectColumnsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class QualifiedSelectColumnsVisitor : DiagnosticVisitorBase
    {
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
            if (tableReferences.Count <= 1)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Check SELECT list for unqualified column references
            if (querySpec.SelectElements != null)
            {
                foreach (var selectElement in querySpec.SelectElements)
                {
                    if (selectElement is SelectScalarExpression scalarExpr)
                    {
                        CheckExpressionForUnqualifiedColumns(scalarExpr.Expression);
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckExpressionForUnqualifiedColumns(ScalarExpression expression)
        {
            if (expression is ColumnReferenceExpression colRef)
            {
                // Check if column is unqualified (no table/alias prefix)
                if (colRef.MultiPartIdentifier != null && colRef.MultiPartIdentifier.Count == 1)
                {
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{colRef.MultiPartIdentifier.Identifiers[0].Value}' should be qualified with table name or alias when multiple tables are present.",
                        code: "qualified-select-columns",
                        category: "Style",
                        fixable: false
                    );
                }
            }
            else if (expression is BinaryExpression binary)
            {
                CheckExpressionForUnqualifiedColumns(binary.FirstExpression);
                CheckExpressionForUnqualifiedColumns(binary.SecondExpression);
            }
            else if (expression is FunctionCall func)
            {
                if (func.Parameters != null)
                {
                    foreach (var param in func.Parameters)
                    {
                        CheckExpressionForUnqualifiedColumns(param);
                    }
                }
            }
            else if (expression is CastCall castCall)
            {
                CheckExpressionForUnqualifiedColumns(castCall.Parameter);
            }
            else if (expression is ConvertCall convertCall)
            {
                CheckExpressionForUnqualifiedColumns(convertCall.Parameter);
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
