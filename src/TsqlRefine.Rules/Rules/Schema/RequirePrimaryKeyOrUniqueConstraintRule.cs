using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity.
/// </summary>
public sealed class RequirePrimaryKeyOrUniqueConstraintRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-primary-key-or-unique-constraint",
        Description: "Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequirePrimaryKeyOrUniqueConstraintVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequirePrimaryKeyOrUniqueConstraintVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Skip temporary tables (#temp, ##temp)
            if (node.SchemaObjectName?.BaseIdentifier is not { } tableIdentifier ||
                ScriptDomHelpers.IsTemporaryTableName(tableIdentifier.Value))
            {
                base.ExplicitVisit(node);
                return;
            }

            bool hasPrimaryKey = false;
            bool hasUniqueConstraint = false;

            // Check table constraints
            if (node.Definition?.TableConstraints != null)
            {
                foreach (var constraint in node.Definition.TableConstraints)
                {
                    if (constraint is UniqueConstraintDefinition uniqueConstraint)
                    {
                        if (uniqueConstraint.IsPrimaryKey)
                        {
                            hasPrimaryKey = true;
                        }
                        else
                        {
                            hasUniqueConstraint = true;
                        }
                    }
                }
            }

            // Check column constraints
            if (!hasPrimaryKey && !hasUniqueConstraint && node.Definition?.ColumnDefinitions != null)
            {
                foreach (var column in node.Definition.ColumnDefinitions)
                {
                    if (column.Constraints != null)
                    {
                        foreach (var constraint in column.Constraints)
                        {
                            if (constraint is UniqueConstraintDefinition uniqueConstraint)
                            {
                                if (uniqueConstraint.IsPrimaryKey)
                                {
                                    hasPrimaryKey = true;
                                }
                                else
                                {
                                    hasUniqueConstraint = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!hasPrimaryKey && !hasUniqueConstraint)
            {
                AddDiagnostic(
                    fragment: tableIdentifier,
                    message: "Table should have a PRIMARY KEY or UNIQUE constraint to enforce entity integrity and support relational operations.",
                    code: "require-primary-key-or-unique-constraint",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
