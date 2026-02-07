using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

public sealed class DangerousDdlRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "dangerous-ddl",
        Description: "Detects destructive DDL operations (DROP, TRUNCATE, ALTER TABLE DROP) that can cause irreversible data loss.",
        Category: "Safety",
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

        var visitor = new DangerousDdlVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DangerousDdlVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DropDatabaseStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "DROP DATABASE is a destructive operation that causes catastrophic data loss. This should be reviewed carefully.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropTableStatement node)
        {
            // Skip temp tables (# and ##)
            if (node.Objects != null)
            {
                foreach (var obj in node.Objects)
                {
                    if (obj.BaseIdentifier != null)
                    {
                        var tableName = obj.BaseIdentifier.Value;
                        if (tableName.StartsWith('#'))
                        {
                            // Temp table - skip warning
                            continue;
                        }

                        AddDiagnostic(
                            fragment: node,
                            message: "DROP TABLE is a destructive operation that causes irreversible data loss. Ensure this is intentional.",
                            code: "dangerous-ddl",
                            category: "Safety",
                            fixable: false,
                            severity: node.IsIfExists ? DiagnosticSeverity.Information : null
                        );
                        break; // Only report once per statement
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropProcedureStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "DROP PROCEDURE is a destructive operation. Ensure this is intentional and the procedure is versioned/backed up.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false,
                severity: node.IsIfExists ? DiagnosticSeverity.Information : null
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropViewStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "DROP VIEW is a destructive operation. Ensure this is intentional and the view definition is versioned/backed up.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false,
                severity: node.IsIfExists ? DiagnosticSeverity.Information : null
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropFunctionStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "DROP FUNCTION is a destructive operation. Ensure this is intentional and the function definition is versioned/backed up.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false,
                severity: node.IsIfExists ? DiagnosticSeverity.Information : null
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(TruncateTableStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "TRUNCATE TABLE is a destructive operation that removes all rows and cannot be rolled back in all scenarios. Ensure this is intentional.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterTableDropTableElementStatement node)
        {
            // This covers DROP COLUMN and DROP CONSTRAINT
            AddDiagnostic(
                fragment: node,
                message: "ALTER TABLE DROP is a destructive schema change that can cause data loss or break dependent objects. Ensure this is intentional.",
                code: "dangerous-ddl",
                category: "Safety",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
