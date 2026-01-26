using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class RequirePrimaryKeyOrUniqueConstraintRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-primary-key-or-unique-constraint",
        Description: "Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity.",
        Category: "Schema Design",
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

        var visitor = new RequirePrimaryKeyOrUniqueConstraintVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequirePrimaryKeyOrUniqueConstraintVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
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
                    fragment: node,
                    message: "Table should have a PRIMARY KEY or UNIQUE constraint to enforce entity integrity and support relational operations.",
                    code: "require-primary-key-or-unique-constraint",
                    category: "Schema Design",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
