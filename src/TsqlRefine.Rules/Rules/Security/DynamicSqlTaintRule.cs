using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.ControlFlow;

namespace TsqlRefine.Rules.Rules.Security;

/// <summary>Tracks untrusted values through assignments into dynamic SQL execution sinks.</summary>
public sealed class DynamicSqlTaintRule : ControlFlowRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "dynamic-sql-taint",
        "Detects untrusted values that flow into dynamically executed SQL text.",
        "Security",
        RuleSeverity.Error,
        false);

    private protected override IEnumerable<ControlFlowIssue> AnalyzeScope(ControlFlowScope scope)
    {
        var analysis = new SqlTaintAnalysis(scope);
        var states = analysis.Solve(scope.Graph);
        var issues = new List<ControlFlowIssue>();
        foreach (var node in scope.Graph.Nodes)
        {
            if (node.Statement is not ExecuteStatement execute ||
                !states.TryGetValue(node, out var state))
            {
                continue;
            }

            foreach (var sqlExpression in GetDynamicSqlExpressions(execute))
            {
                var value = analysis.Evaluate(sqlExpression, state);
                var safety = value.GetSqlTextSafety();
                if (safety != SqlTextSafety.Safe)
                {
                    issues.Add(new ControlFlowIssue(
                        sqlExpression,
                        safety == SqlTextSafety.Unknown
                            ? "Dynamic SQL safety could not be proven because the SQL text contains a value from an unsupported or indeterminate source. Review the value's origin and ensure identifiers are quoted and data values are parameterized."
                            : "Dynamic SQL text contains an untrusted or incorrectly escaped value. Use sp_executesql parameters, QUOTENAME for identifiers, or context-appropriate escaping.",
                        safety == SqlTextSafety.Unknown ? DiagnosticSeverity.Warning : null));
                    break;
                }
            }
        }
        return issues;
    }

    private static IEnumerable<ScalarExpression> GetDynamicSqlExpressions(ExecuteStatement statement)
    {
        switch (statement.ExecuteSpecification?.ExecutableEntity)
        {
            case ExecutableStringList strings:
                foreach (var expression in strings.Strings)
                {
                    yield return expression;
                }
                break;
            case ExecutableProcedureReference procedure when
                IsSpExecutesql(procedure) && procedure.Parameters is { Count: > 0 }:
                if (procedure.Parameters[0].ParameterValue is { } parameterValue)
                {
                    yield return parameterValue;
                }
                break;
        }
    }

    private static bool IsSpExecutesql(ExecutableProcedureReference procedure)
    {
        var name = procedure.ProcedureReference?.ProcedureReference?.Name.BaseIdentifier?.Value;
        return string.Equals(name, "sp_executesql", StringComparison.OrdinalIgnoreCase);
    }
}
