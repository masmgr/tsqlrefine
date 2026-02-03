using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers.Transformation;

/// <summary>
/// Maps source positions to operator context information derived from AST analysis.
/// Enables accurate distinction between binary/unary operators and different asterisk contexts.
/// </summary>
public sealed class AstPositionMap
{
    /// <summary>
    /// Represents the semantic context of an operator at a specific position.
    /// </summary>
    public enum OperatorContext
    {
        /// <summary>AST could not determine context (fall back to heuristics).</summary>
        Unknown,

        /// <summary>Binary arithmetic operator: a + b, a * b, a - b, etc.</summary>
        BinaryArithmetic,

        /// <summary>Unary sign operator: -1, +1.</summary>
        UnarySign,

        /// <summary>SELECT * without table qualifier.</summary>
        SelectStar,

        /// <summary>SELECT t.* with table qualifier.</summary>
        QualifiedStar,

        /// <summary>Asterisk in function call: COUNT(*), SUM(*), etc.</summary>
        FunctionStar,

        /// <summary>Comparison operator: =, &lt;&gt;, &lt;, &gt;, etc.</summary>
        Comparison
    }

    private readonly Dictionary<(int line, int column), OperatorContext> _contexts;

    private AstPositionMap(Dictionary<(int line, int column), OperatorContext> contexts)
    {
        _contexts = contexts;
    }

    /// <summary>
    /// Builds an operator position map from the given AST fragment.
    /// </summary>
    /// <param name="fragment">The parsed SQL fragment. Can be null if parsing failed.</param>
    /// <returns>A position map, or null if fragment is null.</returns>
    public static AstPositionMap? Build(TSqlFragment? fragment)
    {
        if (fragment is null)
        {
            return null;
        }

        var visitor = new OperatorContextVisitor();
        fragment.Accept(visitor);
        return new AstPositionMap(visitor.Contexts);
    }

    /// <summary>
    /// Gets the operator context at the specified position.
    /// </summary>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
    /// <returns>The operator context, or Unknown if not found.</returns>
    public OperatorContext GetContext(int line, int column)
    {
        return _contexts.TryGetValue((line, column), out var context)
            ? context
            : OperatorContext.Unknown;
    }

    /// <summary>
    /// Visitor that collects operator positions and their contexts from the AST.
    /// </summary>
    private sealed class OperatorContextVisitor : TSqlFragmentVisitor
    {
        public Dictionary<(int line, int column), OperatorContext> Contexts { get; } = [];

        public override void ExplicitVisit(BinaryExpression node)
        {
            // BinaryExpression represents arithmetic binary operations like a + b, a * b
            // The operator position is between FirstExpression and SecondExpression
            // We need to calculate the operator position from the gap between them
            var firstEnd = node.FirstExpression.StartColumn + node.FirstExpression.FragmentLength;
            var secondStart = node.SecondExpression.StartColumn;

            // The operator is somewhere between first and second expression
            // For single-char operators, it's typically at or near the midpoint
            // We record the position based on the operator type
            var operatorPosition = FindOperatorPosition(node);
            if (operatorPosition.HasValue)
            {
                Contexts[(node.StartLine, operatorPosition.Value)] = OperatorContext.BinaryArithmetic;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UnaryExpression node)
        {
            // UnaryExpression represents unary +/- operators
            // The operator is at the start of the expression
            Contexts[(node.StartLine, node.StartColumn)] = OperatorContext.UnarySign;

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            // Comparison operators: =, <>, <, >, <=, >=, !=
            var operatorPosition = FindComparisonOperatorPosition(node);
            if (operatorPosition.HasValue)
            {
                Contexts[(node.StartLine, operatorPosition.Value)] = OperatorContext.Comparison;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectStarExpression node)
        {
            // SELECT * or SELECT t.*
            // Check if it has a qualifier (table alias)
            if (node.Qualifier is not null && node.Qualifier.Identifiers.Count > 0)
            {
                // Qualified: t.* - the asterisk is after the dot
                // Position is at the end of the qualifier plus the dot
                var qualifierEnd = node.Qualifier.StartColumn + node.Qualifier.FragmentLength;
                // The * is right after the qualifier (including the dot)
                Contexts[(node.StartLine, qualifierEnd)] = OperatorContext.QualifiedStar;
            }
            else
            {
                // Unqualified: SELECT *
                Contexts[(node.StartLine, node.StartColumn)] = OperatorContext.SelectStar;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            // Check for COUNT(*), SUM(*), etc.
            // FunctionCall with * parameter can be represented in multiple ways:
            // 1. UniqueRowFilter = All (for DISTINCT, etc.)
            // 2. Parameters contains a ColumnReferenceExpression with ColumnType.Wildcard
            // 3. Parameters.Count == 0 but source contains * (heuristic)
            var hasWildcardParam = node.Parameters.Count == 1 &&
                node.Parameters[0] is ColumnReferenceExpression colRef &&
                colRef.ColumnType == ColumnType.Wildcard;

            if (hasWildcardParam || node.UniqueRowFilter == UniqueRowFilter.All ||
                (node.Parameters.Count == 0 && HasAsteriskInSource(node)))
            {
                // Find the asterisk position inside the parentheses
                // It's typically at StartColumn + FunctionName.Length + 1 (for the opening paren)
                var funcNameLen = node.FunctionName?.Value?.Length ?? 0;
                var asteriskColumn = node.StartColumn + funcNameLen + 1; // +1 for '('
                Contexts[(node.StartLine, asteriskColumn)] = OperatorContext.FunctionStar;
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Finds the position of the arithmetic operator in a BinaryExpression.
        /// </summary>
        private static int? FindOperatorPosition(BinaryExpression node)
        {
            // The operator is between FirstExpression and SecondExpression
            // Calculate position: end of first expression, skip whitespace, find operator
            var firstExprEnd = node.FirstExpression.StartColumn + node.FirstExpression.FragmentLength;
            var secondExprStart = node.SecondExpression.StartColumn;

            // The operator is somewhere in the gap
            // For simple cases, estimate it's right after the first expression (plus possible space)
            // Since we can't access the original source here, we use an approximation:
            // The operator is typically 1-2 characters before the second expression
            if (secondExprStart > firstExprEnd)
            {
                // Account for spaces: operator is usually at midpoint or just before second expr
                // For single-char operators (+, -, *, /, %), position is (secondExprStart - 2) to (secondExprStart - 1)
                return secondExprStart - 1;
            }

            return null;
        }

        /// <summary>
        /// Finds the position of the comparison operator in a BooleanComparisonExpression.
        /// </summary>
        private static int? FindComparisonOperatorPosition(BooleanComparisonExpression node)
        {
            var firstExprEnd = node.FirstExpression.StartColumn + node.FirstExpression.FragmentLength;
            var secondExprStart = node.SecondExpression.StartColumn;

            if (secondExprStart > firstExprEnd)
            {
                // Similar logic to binary expression
                // For multi-char operators (<>, <=, >=, !=), the position is the start of the operator
                return secondExprStart - GetComparisonOperatorLength(node.ComparisonType);
            }

            return null;
        }

        private static int GetComparisonOperatorLength(BooleanComparisonType comparisonType)
        {
            return comparisonType switch
            {
                BooleanComparisonType.Equals => 1,           // =
                BooleanComparisonType.GreaterThan => 1,      // >
                BooleanComparisonType.LessThan => 1,         // <
                BooleanComparisonType.GreaterThanOrEqualTo => 2, // >=
                BooleanComparisonType.LessThanOrEqualTo => 2,    // <=
                BooleanComparisonType.NotEqualToBrackets => 2,   // <>
                BooleanComparisonType.NotEqualToExclamation => 2, // !=
                _ => 1
            };
        }

        /// <summary>
        /// Checks if the function call has an asterisk parameter.
        /// This is a heuristic check based on fragment content.
        /// </summary>
        private static bool HasAsteriskInSource(FunctionCall node)
        {
            // FunctionCall nodes with COUNT(*) pattern typically have:
            // - No explicit parameters in Parameters collection
            // - But the source contains *
            // We can check by looking at UniqueRowFilter or by fragment length heuristics
            // For COUNT(*), the fragment is "COUNT(*)" which has specific length characteristics
            var funcName = node.FunctionName?.Value;
            if (funcName is null)
            {
                return false;
            }

            // Minimum length for func(*) is funcName.Length + 3 (for "(*)")
            var minLengthForAsterisk = funcName.Length + 3;
            return node.FragmentLength >= minLengthForAsterisk;
        }
    }
}
