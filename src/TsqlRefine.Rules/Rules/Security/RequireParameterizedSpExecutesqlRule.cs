using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Security;

/// <summary>
/// Detects sp_executesql calls without proper parameterization or with string concatenation.
/// </summary>
public sealed class RequireParameterizedSpExecutesqlRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-parameterized-sp-executesql",
        Description: "Detects sp_executesql calls without proper parameterization or with string concatenation.",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireParameterizedSpExecutesqlVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireParameterizedSpExecutesqlVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is not ExecutableProcedureReference procRef)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (!IsSpExecutesql(procRef))
            {
                base.ExplicitVisit(node);
                return;
            }

            var procNameIdentifier = procRef.ProcedureReference!.ProcedureReference!.Name.BaseIdentifier!;
            var parameters = procRef.Parameters;

            // Check if first parameter uses string concatenation
            if (parameters is { Count: > 0 } && ContainsBinaryExpression(parameters[0].ParameterValue))
            {
                AddDiagnostic(
                    fragment: procNameIdentifier,
                    message: "sp_executesql with string concatenation is vulnerable to SQL injection. Use parameterized form: sp_executesql @sql, N'@p type', @p = @value.",
                    code: "require-parameterized-sp-executesql",
                    category: "Security",
                    fixable: false
                );
            }
            // Check if only one parameter (no parameter definitions)
            else if (parameters is null or { Count: <= 1 })
            {
                AddDiagnostic(
                    fragment: procNameIdentifier,
                    message: "sp_executesql without parameter definitions. Add parameter specification to prevent SQL injection.",
                    code: "require-parameterized-sp-executesql",
                    category: "Security",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool IsSpExecutesql(ExecutableProcedureReference procRef)
        {
            var name = procRef.ProcedureReference?.ProcedureReference?.Name;
            if (name is null)
                return false;

            // Check for sp_executesql (may be schema-qualified as sys.sp_executesql)
            var baseName = name.BaseIdentifier?.Value;
            return string.Equals(baseName, "sp_executesql", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsBinaryExpression(ScalarExpression? expression)
        {
            if (expression is null)
                return false;

            if (expression is BinaryExpression)
                return true;

            if (expression is ParenthesisExpression paren)
                return ContainsBinaryExpression(paren.Expression);

            return false;
        }
    }
}
