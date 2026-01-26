using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class AvoidHeapTableRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-heap-table",
        Description: "Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs.",
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

        var visitor = new AvoidHeapTableVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidHeapTableVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            bool hasClusteredIndex = false;

            // Check for clustered primary key constraint
            if (node.Definition?.TableConstraints != null)
            {
                foreach (var constraint in node.Definition.TableConstraints)
                {
                    if (constraint is UniqueConstraintDefinition uniqueConstraint)
                    {
                        if (uniqueConstraint.IsPrimaryKey && uniqueConstraint.Clustered == true)
                        {
                            hasClusteredIndex = true;
                        }
                    }
                }
            }

            // Check column constraints for clustered primary key
            if (!hasClusteredIndex && node.Definition?.ColumnDefinitions != null)
            {
                foreach (var column in node.Definition.ColumnDefinitions)
                {
                    if (column.Constraints != null)
                    {
                        foreach (var constraint in column.Constraints)
                        {
                            if (constraint is UniqueConstraintDefinition uniqueConstraint)
                            {
                                if (uniqueConstraint.IsPrimaryKey && uniqueConstraint.Clustered == true)
                                {
                                    hasClusteredIndex = true;
                                }
                            }
                        }
                    }
                }
            }

            // Check for explicit clustered index definitions
            if (!hasClusteredIndex && node.Definition?.Indexes != null)
            {
                foreach (var index in node.Definition.Indexes)
                {
                    if (index.IndexType?.IndexTypeKind == IndexTypeKind.Clustered ||
                        index.IndexType?.IndexTypeKind == IndexTypeKind.ClusteredColumnStore)
                    {
                        hasClusteredIndex = true;
                    }
                }
            }

            if (!hasClusteredIndex)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Table is created as a heap (no clustered index); consider adding a clustered index to improve performance and reduce fragmentation.",
                    code: "avoid-heap-table",
                    category: "Schema Design",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
