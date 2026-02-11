using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Prohibit named constraints in temp tables to avoid naming conflicts
/// </summary>
public sealed class NamedConstraintRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "named-constraint",
        Description: "Prohibit named constraints in temp tables to avoid naming conflicts",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new NamedConstraintVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class NamedConstraintVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Check if table is a temp table (starts with # or ##)
            if (ScriptDomHelpers.IsTemporaryTableName(node.SchemaObjectName?.BaseIdentifier?.Value))
            {
                // Check for named constraints
                if (node.Definition != null)
                {
                    foreach (var element in node.Definition.TableConstraints)
                    {
                        if (element.ConstraintIdentifier != null)
                        {
                            AddDiagnostic(
                                fragment: element,
                                message: "Named constraint found in temp table. Remove constraint names to avoid naming conflicts.",
                                code: "named-constraint",
                                category: "Correctness",
                                fixable: false
                            );
                        }
                    }

                    foreach (var column in node.Definition.ColumnDefinitions)
                    {
                        if (column.Constraints != null)
                        {
                            foreach (var constraint in column.Constraints)
                            {
                                if (constraint.ConstraintIdentifier != null)
                                {
                                    AddDiagnostic(
                                        fragment: constraint,
                                        message: "Named constraint found in temp table. Remove constraint names to avoid naming conflicts.",
                                        code: "named-constraint",
                                        category: "Correctness",
                                        fixable: false
                                    );
                                }
                            }
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
