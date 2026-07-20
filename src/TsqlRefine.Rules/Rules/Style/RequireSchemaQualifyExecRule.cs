using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Catalog;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Requires schema qualification on EXEC procedure calls (e.g., EXEC dbo.ProcName instead of EXEC ProcName).
/// </summary>
public sealed class RequireSchemaQualifyExecRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "require-schema-qualify-exec";
    private const string Category = "Style";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Requires schema qualification on EXEC procedure calls (e.g., EXEC dbo.ProcName instead of EXEC ProcName).",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireSchemaQualifyExecVisitor();

    private sealed class RequireSchemaQualifyExecVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference procRef)
            {
                // Skip variable-based execution (EXEC @procVar)
                if (procRef.ProcedureReference?.ProcedureVariable != null)
                {
                    base.ExplicitVisit(node);
                    return;
                }

                var procName = procRef.ProcedureReference?.ProcedureReference?.Name;
                if (procName is null)
                {
                    base.ExplicitVisit(node);
                    return;
                }

                var baseName = procName.BaseIdentifier?.Value;
                if (string.IsNullOrEmpty(baseName))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip temp procedures (#TempProc, ##GlobalTempProc)
                if (baseName.StartsWith('#'))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip known system stored procedures
                if (SystemProcedureHelpers.IsKnownSystemProcedure(baseName))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Flag if schema is not specified
                if (procName.SchemaIdentifier is null)
                {
                    AddDiagnostic(
                        fragment: procName,
                        message: $"Procedure '{baseName}' should include schema qualification (e.g., dbo.{baseName}).",
                        code: RuleId,
                        category: Category,
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
