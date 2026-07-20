using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results.
/// </summary>
public sealed class BanLegacyJoinSyntaxRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-legacy-join-syntax",
        Description: "Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compoundAssignmentRanges = GetCompoundAssignmentRanges(context.Ast.Fragment);

        // ScriptDom represents both a legal multiply assignment and the legacy left outer join
        // operator as a single MultiplyEquals token. Exclude the legal AST-backed assignments.
        for (var i = 0; i < context.Tokens.Count; i++)
        {
            var token = context.Tokens[i];

            if (token.Text == "*=" && !IsWithinAnyRange(token.Start, compoundAssignmentRanges))
            {
                yield return CreateDiagnostic(context.Tokens, i, i, "*=", "LEFT");
            }
            else if (token.Text == "=*")
            {
                yield return CreateDiagnostic(context.Tokens, i, i, "=*", "RIGHT");
            }
            // Retain support for token streams supplied by plugins or tests that split the operator.
            else if (token.Text == "*" && i + 1 < context.Tokens.Count && context.Tokens[i + 1].Text == "=")
            {
                yield return CreateDiagnostic(context.Tokens, i, i + 1, "*=", "LEFT");
                i++;
            }
            else if (token.Text == "=" && i + 1 < context.Tokens.Count && context.Tokens[i + 1].Text == "*")
            {
                yield return CreateDiagnostic(context.Tokens, i, i + 1, "=*", "RIGHT");
                i++;
            }
        }
    }

    private static Diagnostic CreateDiagnostic(
        IReadOnlyList<Token> tokens,
        int startIndex,
        int endIndex,
        string operatorText,
        string joinType) =>
        RuleHelpers.CreateDiagnostic(
            range: TokenHelpers.GetTokenRange(tokens, startIndex, endIndex),
            message: $"Legacy outer join syntax '{operatorText}' is deprecated since SQL Server 2000. Use {joinType} JOIN instead.",
            code: "avoid-legacy-join-syntax",
            category: "Correctness",
            fixable: false
        );

    private static IReadOnlyList<TsqlRefine.PluginSdk.Range> GetCompoundAssignmentRanges(TSqlFragment? fragment)
    {
        if (fragment is null)
        {
            return Array.Empty<TsqlRefine.PluginSdk.Range>();
        }

        var visitor = new CompoundAssignmentVisitor();
        fragment.Accept(visitor);
        return visitor.Ranges;
    }

    private static bool IsWithinAnyRange(
        Position position,
        IReadOnlyList<TsqlRefine.PluginSdk.Range> ranges) =>
        ranges.Any(range => Compare(position, range.Start) >= 0 && Compare(position, range.End) < 0);

    private static int Compare(Position left, Position right)
    {
        var lineComparison = left.Line.CompareTo(right.Line);
        return lineComparison != 0 ? lineComparison : left.Character.CompareTo(right.Character);
    }

    private sealed class CompoundAssignmentVisitor : TSqlFragmentVisitor
    {
        public List<TsqlRefine.PluginSdk.Range> Ranges { get; } = [];

        public override void ExplicitVisit(SetVariableStatement node)
        {
            AddMultiplyAssignment(node, node.AssignmentKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            AddMultiplyAssignment(node, node.AssignmentKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AssignmentSetClause node)
        {
            AddMultiplyAssignment(node, node.AssignmentKind);
            base.ExplicitVisit(node);
        }

        private void AddMultiplyAssignment(TSqlFragment node, AssignmentKind assignmentKind)
        {
            if (assignmentKind == AssignmentKind.MultiplyEquals)
            {
                Ranges.Add(ScriptDomHelpers.GetRange(node));
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
