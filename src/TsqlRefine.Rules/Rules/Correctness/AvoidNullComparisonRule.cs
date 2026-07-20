using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects NULL comparisons using = or &lt;&gt; instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.
/// </summary>
public sealed class AvoidNullComparisonRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "avoid-null-comparison";
    private const string Category = "Correctness";
    private static readonly ConditionalWeakTable<TSqlFragment, Issue[]> s_issueCache = new();

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Error,
        Fixable: true
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidNullComparisonVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            return [];
        }

        var issue = FindIssues(context)
            .FirstOrDefault(i => i.Diagnostic.Range == diagnostic.Range);

        if (issue is null)
        {
            return [];
        }

        if (ContainsComment(issue.ComparisonExpression))
        {
            return [];
        }

        var rawSql = context.Ast.RawSql;
        var nonNullExpr = issue.NonNullExpression;
        if (nonNullExpr is NullLiteral)
        {
            // For patterns like "NULL = NULL", any direct rewrite changes semantics.
            return [];
        }

        var exprText = rawSql.Substring(nonNullExpr.StartOffset, nonNullExpr.FragmentLength);

        var replacement = issue.IsEquals
            ? $"{exprText} IS NULL"
            : $"{exprText} IS NOT NULL";

        return [RuleHelpers.CreateReplaceFix("Replace with IS [NOT] NULL", diagnostic.Range, replacement)];
    }

    private sealed record Issue(
        Diagnostic Diagnostic,
        TSqlFragment ComparisonExpression,
        TSqlFragment NonNullExpression,
        bool IsEquals);

    private static bool ContainsComment(TSqlFragment fragment)
    {
        var tokens = fragment.ScriptTokenStream;
        if (tokens is null || fragment.FirstTokenIndex < 0 || fragment.LastTokenIndex >= tokens.Count)
        {
            return false;
        }

        for (var i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
        {
            if (tokens[i].TokenType is TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment)
            {
                return true;
            }
        }

        return false;
    }

    private static Issue[] FindIssues(RuleContext context)
    {
        if (context.Ast.Fragment is null)
        {
            return [];
        }

        return s_issueCache.GetValue(context.Ast.Fragment, static fragment =>
        {
            var visitor = new AvoidNullComparisonVisitor();
            fragment.Accept(visitor);
            return visitor.Issues.ToArray();
        });
    }

    private sealed class AvoidNullComparisonVisitor : DiagnosticVisitorBase
    {
        private readonly List<Issue> _issues = new();

        public IReadOnlyList<Issue> Issues => _issues;

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            var isInvalidComparison = node.ComparisonType is
                BooleanComparisonType.Equals or
                BooleanComparisonType.NotEqualToBrackets or
                BooleanComparisonType.NotEqualToExclamation;

            if (isInvalidComparison)
            {
                var hasNullLiteral = node.FirstExpression is NullLiteral ||
                                    node.SecondExpression is NullLiteral;

                if (hasNullLiteral)
                {
                    var comparisonOperator = node.ComparisonType switch
                    {
                        BooleanComparisonType.Equals => "=",
                        BooleanComparisonType.NotEqualToBrackets => "<>",
                        BooleanComparisonType.NotEqualToExclamation => "!=",
                        _ => "comparison"
                    };

                    var isEquals = node.ComparisonType == BooleanComparisonType.Equals;
                    var suggestedOperator = isEquals ? "IS NULL" : "IS NOT NULL";

                    AddDiagnostic(
                        fragment: node,
                        message: $"NULL comparison using '{comparisonOperator}' always evaluates to UNKNOWN. Use '{suggestedOperator}' instead.",
                        code: RuleId,
                        category: Category,
                        fixable: true
                    );

                    // Determine the non-NULL expression for the fix
                    var nonNullExpression = node.FirstExpression is NullLiteral
                        ? node.SecondExpression
                        : node.FirstExpression;

                    _issues.Add(new Issue(
                        Diagnostic: Diagnostics[^1],
                        ComparisonExpression: node,
                        NonNullExpression: nonNullExpression,
                        IsEquals: isEquals));
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
