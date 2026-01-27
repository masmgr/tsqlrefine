using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Naming;

public sealed class MeaningfulAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "meaningful-alias",
        Description: "Use meaningful aliases instead of single-character aliases in multi-table queries",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new MeaningfulAliasVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MeaningfulAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            // Count table references in this query
            var tableReferences = CountTableReferences(node.FromClause);

            // Only check for meaningful aliases if there are multiple tables
            if (tableReferences > 1)
            {
                CheckTableAliases(node.FromClause);
            }

            base.ExplicitVisit(node);
        }

        private int CountTableReferences(FromClause? fromClause)
        {
            if (fromClause == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var tableRef in fromClause.TableReferences)
            {
                count += CountTableReferencesRecursive(tableRef);
            }

            return count;
        }

        private int CountTableReferencesRecursive(TableReference tableRef)
        {
            return tableRef switch
            {
                NamedTableReference => 1,
                QueryDerivedTable => 1,
                QualifiedJoin join => CountTableReferencesRecursive(join.FirstTableReference) +
                                       CountTableReferencesRecursive(join.SecondTableReference),
                _ => 0
            };
        }

        private void CheckTableAliases(FromClause? fromClause)
        {
            if (fromClause == null)
            {
                return;
            }

            foreach (var tableRef in fromClause.TableReferences)
            {
                CheckTableAliasRecursive(tableRef);
            }
        }

        private void CheckTableAliasRecursive(TableReference tableRef)
        {
            switch (tableRef)
            {
                case NamedTableReference namedTable when namedTable.Alias != null:
                    CheckAlias(namedTable.Alias);
                    break;

                case QueryDerivedTable derivedTable when derivedTable.Alias != null:
                    CheckAlias(derivedTable.Alias);
                    break;

                case QualifiedJoin join:
                    CheckTableAliasRecursive(join.FirstTableReference);
                    CheckTableAliasRecursive(join.SecondTableReference);
                    break;
            }
        }

        private void CheckAlias(Identifier alias)
        {
            // Check if the alias is a single character
            if (alias.Value.Length == 1)
            {
                AddDiagnostic(
                    fragment: alias,
                    message: "Use meaningful aliases instead of single-character aliases in multi-table queries",
                    code: "meaningful-alias",
                    category: "Style",
                    fixable: false
                );
            }
        }
    }
}
