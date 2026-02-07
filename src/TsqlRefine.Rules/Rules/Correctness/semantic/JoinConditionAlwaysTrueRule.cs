using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

public sealed class JoinConditionAlwaysTrueRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/join-condition-always-true",
        Description: "Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons.",
        Category: "Correctness",
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

        var visitor = new JoinConditionAlwaysTrueVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class JoinConditionAlwaysTrueVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (node.SearchCondition != null)
            {
                CheckJoinCondition(node, node.SearchCondition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinCondition(QualifiedJoin join, BooleanExpression condition)
        {
            if (condition is BooleanComparisonExpression comparison)
            {
                // Check for literal comparisons like 1=1, 0=0, 'a'='a'
                if (AreLiteralsEqual(comparison.FirstExpression, comparison.SecondExpression))
                {
                    AddDiagnostic(
                        fragment: join,
                        message: "JOIN condition is always true (e.g., '1=1'). This likely indicates a missing or incorrect join condition and may result in a Cartesian product.",
                        code: "semantic/join-condition-always-true",
                        category: "Correctness",
                        fixable: false
                    );
                    return;
                }

                // Check for self-comparisons like t1.col = t1.col using helper
                if (comparison.FirstExpression is ColumnReferenceExpression firstCol &&
                    comparison.SecondExpression is ColumnReferenceExpression secondCol &&
                    ColumnReferenceHelpers.AreColumnReferencesEqual(firstCol, secondCol))
                {
                    AddDiagnostic(
                        fragment: join,
                        message: "JOIN condition compares a column to itself (e.g., 't1.col = t1.col'). This is always true and likely incorrect.",
                        code: "semantic/join-condition-always-true",
                        category: "Correctness",
                        fixable: false
                    );
                    return;
                }
            }
            else if (condition is BooleanBinaryExpression binaryExpr)
            {
                // Recursively check AND/OR expressions
                CheckJoinCondition(join, binaryExpr.FirstExpression);
                CheckJoinCondition(join, binaryExpr.SecondExpression);
            }
        }

        private static bool AreLiteralsEqual(ScalarExpression? first, ScalarExpression? second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            // Check integer literals
            if (first is IntegerLiteral firstInt && second is IntegerLiteral secondInt)
            {
                return firstInt.Value == secondInt.Value;
            }

            // Check numeric literals
            if (first is NumericLiteral firstNum && second is NumericLiteral secondNum)
            {
                return firstNum.Value == secondNum.Value;
            }

            // Check string literals
            if (first is StringLiteral firstStr && second is StringLiteral secondStr)
            {
                return firstStr.Value == secondStr.Value;
            }

            return false;
        }
    }
}
