using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects loops that use DATALENGTH for termination but LEN to advance the same string variable.
/// The functions treat trailing spaces differently, so the loop may fail to make progress.
/// </summary>
public sealed class MixedStringLengthFunctionsInLoopRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "mixed-string-length-functions-in-loop",
        Description: "Detects WHILE loops that use DATALENGTH for termination but LEN to advance the same string variable.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new MixedStringLengthFunctionsInLoopVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MixedStringLengthFunctionsInLoopVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(WhileStatement node)
        {
            var conditionCollector = new DataLengthConditionVariableCollector();
            node.Predicate?.Accept(conditionCollector);

            if (conditionCollector.VariableNames.Count > 0 && node.Statement is not null)
            {
                var assignmentCollector = new LoopAssignmentCollector(conditionCollector.VariableNames);
                node.Statement.Accept(assignmentCollector);

                foreach (var lenCall in assignmentCollector.UnsafeLenCalls)
                {
                    AddDiagnostic(
                        fragment: lenCall,
                        message: "This loop terminates with DATALENGTH but advances the same string with LEN; trailing-space handling can differ and prevent progress. Use consistent length semantics.",
                        code: metadata.RuleId,
                        category: metadata.Category,
                        fixable: metadata.Fixable);
                }
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed class DataLengthConditionVariableCollector : TSqlFragmentVisitor
    {
        private readonly HashSet<string> _variableNames = new(StringComparer.OrdinalIgnoreCase);

        public FrozenSet<string> VariableNames => _variableNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.FunctionName.Value.Equals("DATALENGTH", StringComparison.OrdinalIgnoreCase) &&
                node.Parameters.Count > 0)
            {
                var collector = new VariableNameCollector();
                node.Parameters[0].Accept(collector);
                _variableNames.UnionWith(collector.VariableNames);
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed class LoopAssignmentCollector(FrozenSet<string> conditionVariables) : TSqlFragmentVisitor
    {
        private readonly HashSet<FunctionCall> _unsafeLenCalls = new(ReferenceEqualityComparer.Instance);

        public IReadOnlyCollection<FunctionCall> UnsafeLenCalls => _unsafeLenCalls;

        public override void ExplicitVisit(WhileStatement node)
        {
            // Nested loops are analyzed independently by the outer diagnostic visitor.
        }

        public override void ExplicitVisit(SetVariableStatement node)
        {
            CheckAssignment(node.Variable?.Name, node.Expression);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            CheckAssignment(node.Variable?.Name, node.Expression);
            base.ExplicitVisit(node);
        }

        private void CheckAssignment(string? variableName, ScalarExpression? expression)
        {
            if (variableName is null || expression is null || !conditionVariables.Contains(variableName))
            {
                return;
            }

            var slicingCollector = new SlicingFunctionCollector(variableName);
            expression.Accept(slicingCollector);
            _unsafeLenCalls.UnionWith(slicingCollector.LenCalls);
        }
    }

    private sealed class SlicingFunctionCollector(string targetVariable) : TSqlFragmentVisitor
    {
        private static readonly FrozenSet<string> SlicingFunctions = FrozenSet.ToFrozenSet(
            ["SUBSTRING", "LEFT", "RIGHT", "STUFF"],
            StringComparer.OrdinalIgnoreCase);

        public List<FunctionCall> LenCalls { get; } = [];

        public override void ExplicitVisit(FunctionCall node)
        {
            if (!SlicingFunctions.Contains(node.FunctionName.Value))
            {
                base.ExplicitVisit(node);
                return;
            }

            var variableCollector = new VariableNameCollector();
            foreach (var parameter in node.Parameters)
            {
                parameter.Accept(variableCollector);
            }
            if (!variableCollector.VariableNames.Contains(targetVariable))
            {
                return;
            }

            var lenCollector = new NamedFunctionCollector("LEN");
            foreach (var parameter in node.Parameters)
            {
                parameter.Accept(lenCollector);
            }
            LenCalls.AddRange(lenCollector.Matches);
        }

        public override void ExplicitVisit(LeftFunctionCall node) =>
            CheckBuiltInSlicingFunction(node.Parameters);

        public override void ExplicitVisit(RightFunctionCall node) =>
            CheckBuiltInSlicingFunction(node.Parameters);

        private void CheckBuiltInSlicingFunction(IList<ScalarExpression> parameters)
        {
            var variableCollector = new VariableNameCollector();
            foreach (var parameter in parameters)
            {
                parameter.Accept(variableCollector);
            }

            if (!variableCollector.VariableNames.Contains(targetVariable))
            {
                return;
            }

            var lenCollector = new NamedFunctionCollector("LEN");
            foreach (var parameter in parameters)
            {
                parameter.Accept(lenCollector);
            }

            LenCalls.AddRange(lenCollector.Matches);
        }
    }

    private sealed class VariableNameCollector : TSqlFragmentVisitor
    {
        public HashSet<string> VariableNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(VariableReference node)
        {
            VariableNames.Add(node.Name);
        }
    }

    private sealed class NamedFunctionCollector(string functionName) : TSqlFragmentVisitor
    {
        public List<FunctionCall> Matches { get; } = [];

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.FunctionName.Value.Equals(functionName, StringComparison.OrdinalIgnoreCase))
            {
                Matches.Add(node);
            }

            base.ExplicitVisit(node);
        }
    }
}
