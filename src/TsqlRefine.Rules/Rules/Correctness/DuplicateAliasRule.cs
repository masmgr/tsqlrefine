using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class DuplicateAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/duplicate-alias",
        Description: "Detects duplicate table aliases in the same scope, which causes ambiguous references.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new DuplicateAliasVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;
            if (querySpec?.FromClause == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Track aliases we've seen (case-insensitive for SQL Server)
            var seenAliases = new Dictionary<string, TableReference>(StringComparer.OrdinalIgnoreCase);

            // Collect all table references from the FROM clause
            var tableReferences = new List<TableReference>();
            CollectTableReferences(querySpec.FromClause.TableReferences, tableReferences);

            // Check for duplicates
            foreach (var tableRef in tableReferences)
            {
                var alias = GetAliasOrTableName(tableRef);
                if (alias == null)
                {
                    continue;
                }

                if (seenAliases.TryGetValue(alias, out var firstOccurrence))
                {
                    // Found a duplicate
                    AddDiagnostic(
                        fragment: tableRef,
                        message: $"Duplicate table alias '{alias}' in same scope. Each alias must be unique within a FROM clause.",
                        code: "semantic/duplicate-alias",
                        category: "Correctness",
                        fixable: false
                    );
                }
                else
                {
                    seenAliases[alias] = tableRef;
                }
            }

            base.ExplicitVisit(node);
        }

        private static void CollectTableReferences(IList<TableReference> tableRefs, List<TableReference> collected)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    // Recursively collect from both sides of the JOIN
                    var leftRefs = new List<TableReference> { join.FirstTableReference };
                    CollectTableReferences(leftRefs, collected);

                    var rightRefs = new List<TableReference> { join.SecondTableReference };
                    CollectTableReferences(rightRefs, collected);
                }
                else
                {
                    // This is a leaf table reference (NamedTableReference, QueryDerivedTable, etc.)
                    collected.Add(tableRef);
                }
            }
        }

        private static string? GetAliasOrTableName(TableReference tableRef)
        {
            return tableRef switch
            {
                NamedTableReference namedTable =>
                    namedTable.Alias?.Value ?? namedTable.SchemaObject.BaseIdentifier.Value,
                QueryDerivedTable derivedTable =>
                    derivedTable.Alias?.Value,
                _ => null
            };
        }
    }
}
