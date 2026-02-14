using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects optional parameter patterns (@p IS NULL OR col = @p) and (col = ISNULL(@p, col))
/// which prevent index usage and cause plan instability.
/// </summary>
public sealed class AvoidOptionalParameterPatternRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-optional-parameter-pattern",
        Description: "Detects optional parameter patterns (@p IS NULL OR col = @p) and (col = ISNULL(@p, col)) which prevent index usage and cause plan instability.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidOptionalParameterPatternVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidOptionalParameterPatternVisitor : PredicateAwareVisitorBase
    {
        // Pattern A: (@p IS NULL OR col = @p)
        public override void ExplicitVisit(BooleanBinaryExpression node)
        {
            if (IsInPredicate && node.BinaryExpressionType == BooleanBinaryExpressionType.Or)
            {
                if (IsOptionalParameterOrPattern(node.FirstExpression, node.SecondExpression)
                    || IsOptionalParameterOrPattern(node.SecondExpression, node.FirstExpression))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "Optional parameter pattern prevents index usage and causes plan instability. Consider dynamic SQL with parameters or OPTION (RECOMPILE).",
                        code: "avoid-optional-parameter-pattern",
                        category: "Performance",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        // Pattern B: col = ISNULL(@p, col)
        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (IsInPredicate
                && node.ComparisonType == BooleanComparisonType.Equals
                && IsIsnullOptionalPattern(node.FirstExpression, node.SecondExpression))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Optional parameter pattern prevents index usage and causes plan instability. Consider dynamic SQL with parameters or OPTION (RECOMPILE).",
                    code: "avoid-optional-parameter-pattern",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks: isNullSide is "@p IS NULL" and comparisonSide is "col = @p" where @p is the same variable.
        /// </summary>
        private static bool IsOptionalParameterOrPattern(
            BooleanExpression isNullSide,
            BooleanExpression comparisonSide)
        {
            // isNullSide must be: @p IS NULL (not IS NOT NULL)
            if (isNullSide is not BooleanIsNullExpression { IsNot: false } isNullExpr)
                return false;

            if (isNullExpr.Expression is not VariableReference isNullVar)
                return false;

            // comparisonSide must be: col = @p (equality comparison)
            if (comparisonSide is not BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison)
                return false;

            var varName = isNullVar.Name;
            var leftIsVar = HasMatchingVariable(comparison.FirstExpression, varName);
            var rightIsVar = HasMatchingVariable(comparison.SecondExpression, varName);

            // Must be "column = @p" or "@p = column" to avoid false positives like "@x = @p".
            return (leftIsVar && IsColumnReference(comparison.SecondExpression))
                || (rightIsVar && IsColumnReference(comparison.FirstExpression));
        }

        /// <summary>
        /// Checks: col = ISNULL(@p, col) or ISNULL(@p, col) = col
        /// where the ISNULL second argument matches the other side column.
        /// </summary>
        private static bool IsIsnullOptionalPattern(
            ScalarExpression left,
            ScalarExpression right)
        {
            return IsIsnullWithMatchingColumn(left, right)
                || IsIsnullWithMatchingColumn(right, left);
        }

        private static bool IsIsnullWithMatchingColumn(ScalarExpression isnullSide, ScalarExpression columnSide)
        {
            if (isnullSide is not FunctionCall func)
                return false;

            if (!string.Equals(func.FunctionName?.Value, "ISNULL", StringComparison.OrdinalIgnoreCase))
                return false;

            if (func.Parameters is not { Count: 2 })
                return false;

            // First parameter should be a variable
            if (func.Parameters[0] is not VariableReference)
                return false;

            // Second parameter should match the column on the other side
            return AreSameColumnReference(func.Parameters[1], columnSide);
        }

        private static bool HasMatchingVariable(ScalarExpression expression, string variableName) =>
            expression is VariableReference varRef
            && string.Equals(varRef.Name, variableName, StringComparison.OrdinalIgnoreCase);

        private static bool IsColumnReference(ScalarExpression expression) =>
            expression is ColumnReferenceExpression { MultiPartIdentifier.Identifiers.Count: > 0 };

        private static bool AreSameColumnReference(ScalarExpression a, ScalarExpression b)
        {
            if (a is not ColumnReferenceExpression colA || b is not ColumnReferenceExpression colB)
                return false;

            var identifiersA = colA.MultiPartIdentifier?.Identifiers;
            var identifiersB = colB.MultiPartIdentifier?.Identifiers;

            if (identifiersA is null || identifiersB is null)
                return false;

            if (identifiersA.Count != identifiersB.Count)
                return false;

            for (var i = 0; i < identifiersA.Count; i++)
            {
                var nameA = identifiersA[i].Value;
                var nameB = identifiersB[i].Value;
                if (!string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
