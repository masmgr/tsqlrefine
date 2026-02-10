using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap.
/// </summary>
public sealed class QualifiedSelectColumnsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "qualified-select-columns",
        Description: "Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new QualifiedSelectColumnsVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
            TableReferenceHelpers.CollectTableReferences(querySpec.FromClause.TableReferences, tableReferences);

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

        private void CheckExpressionForUnqualifiedColumns(TSqlFragment? fragment)
        {
            if (fragment is null)
            {
                return;
            }

            if (fragment is ScalarSubquery)
            {
                return;
            }

            if (fragment is ColumnReferenceExpression colRef)
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
            else if (fragment is IIfCall iifCall)
            {
                CheckBooleanExpressionForUnqualifiedColumns(iifCall.Predicate);
                CheckExpressionForUnqualifiedColumns(iifCall.ThenExpression);
                CheckExpressionForUnqualifiedColumns(iifCall.ElseExpression);
            }
            else if (fragment is SearchedCaseExpression searchedCase)
            {
                if (searchedCase.WhenClauses != null)
                {
                    foreach (var whenClause in searchedCase.WhenClauses)
                    {
                        CheckBooleanExpressionForUnqualifiedColumns(whenClause.WhenExpression);
                        CheckExpressionForUnqualifiedColumns(whenClause.ThenExpression);
                    }
                }

                CheckExpressionForUnqualifiedColumns(searchedCase.ElseExpression);
            }
            else if (fragment is SimpleCaseExpression simpleCase)
            {
                CheckExpressionForUnqualifiedColumns(simpleCase.InputExpression);

                if (simpleCase.WhenClauses != null)
                {
                    foreach (var whenClause in simpleCase.WhenClauses)
                    {
                        CheckExpressionForUnqualifiedColumns(whenClause.WhenExpression);
                        CheckExpressionForUnqualifiedColumns(whenClause.ThenExpression);
                    }
                }

                CheckExpressionForUnqualifiedColumns(simpleCase.ElseExpression);
            }
            else if (fragment is BinaryExpression binary)
            {
                CheckExpressionForUnqualifiedColumns(binary.FirstExpression);
                CheckExpressionForUnqualifiedColumns(binary.SecondExpression);
            }
            else if (fragment is FunctionCall func)
            {
                if (func.Parameters != null)
                {
                    for (var i = 0; i < func.Parameters.Count; i++)
                    {
                        var param = func.Parameters[i];
                        if (DatePartHelper.IsDatePartLiteralParameter(func, i, param))
                        {
                            continue;
                        }

                        CheckExpressionForUnqualifiedColumns(param);
                    }
                }
            }
            else if (fragment is CastCall castCall)
            {
                CheckExpressionForUnqualifiedColumns(castCall.Parameter);
            }
            else if (fragment is ConvertCall convertCall)
            {
                CheckExpressionForUnqualifiedColumns(convertCall.Parameter);
            }
            else if (fragment is ParenthesisExpression parenthesis)
            {
                CheckExpressionForUnqualifiedColumns(parenthesis.Expression);
            }
            else if (fragment is UnaryExpression unary)
            {
                CheckExpressionForUnqualifiedColumns(unary.Expression);
            }
        }

        private void CheckBooleanExpressionForUnqualifiedColumns(BooleanExpression? expression)
        {
            if (expression is null)
            {
                return;
            }

            if (expression is InPredicate inPredicate)
            {
                CheckExpressionForUnqualifiedColumns(inPredicate.Expression);
                return;
            }

            if (expression is BooleanComparisonExpression comparison)
            {
                CheckExpressionForUnqualifiedColumns(comparison.FirstExpression);
                CheckExpressionForUnqualifiedColumns(comparison.SecondExpression);
                return;
            }

            if (expression is BooleanBinaryExpression binary)
            {
                CheckBooleanExpressionForUnqualifiedColumns(binary.FirstExpression);
                CheckBooleanExpressionForUnqualifiedColumns(binary.SecondExpression);
                return;
            }

            if (expression is BooleanParenthesisExpression parenthesis)
            {
                CheckBooleanExpressionForUnqualifiedColumns(parenthesis.Expression);
                return;
            }

            if (expression is BooleanNotExpression notExpression)
            {
                CheckBooleanExpressionForUnqualifiedColumns(notExpression.Expression);
                return;
            }

            if (expression is BooleanIsNullExpression isNull)
            {
                CheckExpressionForUnqualifiedColumns(isNull.Expression);
                return;
            }

            if (expression is LikePredicate like)
            {
                CheckExpressionForUnqualifiedColumns(like.FirstExpression);
                CheckExpressionForUnqualifiedColumns(like.SecondExpression);
                return;
            }

        }

        // Date-part handling has been moved to DatePartHelper.
    }
}
