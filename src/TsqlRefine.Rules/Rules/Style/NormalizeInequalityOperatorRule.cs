using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Normalizes != to &lt;&gt; (ISO standard inequality operator).
/// </summary>
public sealed class NormalizeInequalityOperatorRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "normalize-inequality-operator";
    private const string Category = "Style";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Normalizes != to <> (ISO standard inequality operator).",
        Category: Category,
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new NormalizeInequalityOperatorVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, RuleId))
        {
            return [];
        }

        var issue = FindIssues(context)
            .FirstOrDefault(i => i.Diagnostic.Range == diagnostic.Range);

        if (issue is null)
        {
            return [];
        }

        var rawSql = context.Ast.RawSql;
        var left = issue.Expression.FirstExpression;
        var right = issue.Expression.SecondExpression;
        var leftText = rawSql.Substring(left.StartOffset, left.FragmentLength);
        var rightText = rawSql.Substring(right.StartOffset, right.FragmentLength);

        return [RuleHelpers.CreateReplaceFix("Replace '!=' with '<>'", diagnostic.Range, $"{leftText} <> {rightText}")];
    }

    private sealed record Issue(Diagnostic Diagnostic, BooleanComparisonExpression Expression);

    private static IReadOnlyList<Issue> FindIssues(RuleContext context)
    {
        if (context.Ast.Fragment is null)
        {
            return [];
        }

        var visitor = new NormalizeInequalityOperatorVisitor();
        context.Ast.Fragment.Accept(visitor);
        return visitor.Issues;
    }

    private sealed class NormalizeInequalityOperatorVisitor : DiagnosticVisitorBase
    {
        private readonly List<Issue> _issues = [];

        public IReadOnlyList<Issue> Issues => _issues;

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (node.ComparisonType == BooleanComparisonType.NotEqualToExclamation)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Use '<>' instead of '!=' for ISO standard compliance.",
                    code: RuleId,
                    category: Category,
                    fixable: true
                );

                _issues.Add(new Issue(
                    Diagnostic: Diagnostics[^1],
                    Expression: node));
            }

            base.ExplicitVisit(node);
        }
    }
}
