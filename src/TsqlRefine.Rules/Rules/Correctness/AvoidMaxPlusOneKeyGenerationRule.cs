using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects MAX(column) + positive integer patterns in assignments and DML values.
/// Such expressions are commonly used for key allocation and are unsafe under concurrency.
/// </summary>
public sealed class AvoidMaxPlusOneKeyGenerationRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-max-plus-one-key-generation",
        Description: "Detects MAX(...) plus a positive integer in assignments or DML values, which is unsafe for key generation.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidMaxPlusOneKeyGenerationVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidMaxPlusOneKeyGenerationVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        private readonly HashSet<BinaryExpression> _reported = new(ReferenceEqualityComparer.Instance);

        public override void ExplicitVisit(SetVariableStatement node)
        {
            CheckExpression(node.Expression);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            CheckExpression(node.Expression);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AssignmentSetClause node)
        {
            CheckExpression(node.NewValue);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            switch (node.InsertSpecification?.InsertSource)
            {
                case ValuesInsertSource values:
                    foreach (var row in values.RowValues)
                    {
                        foreach (var value in row.ColumnValues)
                        {
                            CheckExpression(value);
                        }
                    }

                    break;

                case SelectInsertSource select:
                    var collector = new SelectProjectionCollector();
                    select.Select.Accept(collector);
                    foreach (var expression in collector.Expressions)
                    {
                        CheckExpression(expression);
                    }

                    break;
            }

            base.ExplicitVisit(node);
        }

        private void CheckExpression(ScalarExpression? expression)
        {
            if (expression is null)
            {
                return;
            }

            var finder = new MaxPlusPositiveIntegerFinder();
            expression.Accept(finder);

            foreach (var match in finder.Matches)
            {
                if (!_reported.Add(match))
                {
                    continue;
                }

                AddDiagnostic(
                    fragment: match,
                    message: "MAX(...) plus a positive integer is unsafe for key generation under concurrency and can skip to an unusable range; use SEQUENCE, IDENTITY, or a serialized allocator.",
                    code: metadata.RuleId,
                    category: metadata.Category,
                    fixable: metadata.Fixable);
            }
        }
    }

    private sealed class MaxPlusPositiveIntegerFinder : TSqlFragmentVisitor
    {
        public List<BinaryExpression> Matches { get; } = [];

        public override void ExplicitVisit(BinaryExpression node)
        {
            if (node.BinaryExpressionType == BinaryExpressionType.Add &&
                ((IsPositiveInteger(node.FirstExpression) && ContainsMax(node.SecondExpression)) ||
                 (IsPositiveInteger(node.SecondExpression) && ContainsMax(node.FirstExpression))))
            {
                Matches.Add(node);
                return;
            }

            base.ExplicitVisit(node);
        }

        private static bool IsPositiveInteger(ScalarExpression expression)
        {
            while (expression is ParenthesisExpression parenthesis)
            {
                expression = parenthesis.Expression;
            }

            return expression is IntegerLiteral literal &&
                long.TryParse(literal.Value, out var value) &&
                value > 0;
        }

        private static bool ContainsMax(ScalarExpression expression)
        {
            var finder = new MaxFunctionFinder();
            expression.Accept(finder);
            return finder.Found;
        }
    }

    private sealed class MaxFunctionFinder : TSqlFragmentVisitor
    {
        public bool Found { get; private set; }

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.FunctionName.Value.Equals("MAX", StringComparison.OrdinalIgnoreCase))
            {
                Found = true;
                return;
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed class SelectProjectionCollector : TSqlFragmentVisitor
    {
        public List<ScalarExpression> Expressions { get; } = [];

        public override void ExplicitVisit(SelectScalarExpression node)
        {
            if (node.Expression is not null)
            {
                Expressions.Add(node.Expression);
            }

            // Do not descend into scalar subqueries here. CheckExpression handles any
            // scalar subquery that is itself the projected value.
        }
    }
}
