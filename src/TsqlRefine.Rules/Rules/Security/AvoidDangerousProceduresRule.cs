using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Security;

/// <summary>
/// Detects usage of dangerous extended stored procedures (xp_cmdshell, xp_reg*, sp_OA*) that pose security risks.
/// </summary>
public sealed class AvoidDangerousProceduresRule : IRule
{
    private static readonly FrozenSet<string> s_dangerousProcedures = new[]
    {
        // OS command execution
        "xp_cmdshell",
        // Registry manipulation
        "xp_regread",
        "xp_regwrite",
        "xp_regdeletekey",
        "xp_regdeletevalue",
        "xp_regaddmultistring",
        "xp_regremovemultistring",
        // OLE Automation
        "sp_OACreate",
        "sp_OAMethod",
        "sp_OAGetProperty",
        "sp_OASetProperty",
        "sp_OADestroy",
        "sp_OAGetErrorInfo",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-dangerous-procedures",
        Description: "Detects usage of dangerous extended stored procedures (xp_cmdshell, xp_reg*, sp_OA*) that pose security risks.",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new AvoidDangerousProceduresVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidDangerousProceduresVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference procRef)
            {
                var procName = procRef.ProcedureReference?.ProcedureReference?.Name;
                if (procName != null)
                {
                    // Get the last identifier (the actual procedure name, ignoring schema/database qualifiers)
                    var baseName = procName.Identifiers[^1].Value;

                    if (s_dangerousProcedures.Contains(baseName))
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: $"Avoid using '{baseName}'. Extended stored procedures like xp_cmdshell, xp_reg*, and sp_OA* pose security risks by enabling OS command execution, registry manipulation, or COM object automation.",
                            code: "avoid-dangerous-procedures",
                            category: "Security",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
