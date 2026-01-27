using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class CteNameConflictRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/cte-name-conflict",
        Description: "Detects CTE name conflicts with other CTEs or table aliases in the same scope.",
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

        var visitor = new CteNameConflictVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CteNameConflictVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            // Check for CTE conflicts
            if (node.WithCtesAndXmlNamespaces?.CommonTableExpressions != null)
            {
                CheckCteDuplicates(node.WithCtesAndXmlNamespaces.CommonTableExpressions);
                CheckCteTableAliasConflicts(node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckCteDuplicates(IList<CommonTableExpression> ctes)
        {
            var seenCteNames = new Dictionary<string, CommonTableExpression>(StringComparer.OrdinalIgnoreCase);

            foreach (var cte in ctes)
            {
                var cteName = cte.ExpressionName?.Value;
                if (cteName == null)
                {
                    continue;
                }

                if (seenCteNames.ContainsKey(cteName))
                {
                    // Found a duplicate CTE name
                    AddDiagnostic(
                        fragment: cte,
                        message: $"Duplicate CTE name '{cteName}'. Each CTE name must be unique within a WITH clause.",
                        code: "semantic/cte-name-conflict",
                        category: "Correctness",
                        fixable: false
                    );
                }
                else
                {
                    seenCteNames[cteName] = cte;
                }
            }
        }

        private void CheckCteTableAliasConflicts(SelectStatement node)
        {
            if (node.WithCtesAndXmlNamespaces?.CommonTableExpressions == null)
            {
                return;
            }

            // Collect CTE names
            var cteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cte in node.WithCtesAndXmlNamespaces.CommonTableExpressions)
            {
                if (cte.ExpressionName?.Value != null)
                {
                    cteNames.Add(cte.ExpressionName.Value);
                }
            }

            // Collect table aliases from the main query
            if (node.QueryExpression is QuerySpecification querySpec && querySpec.FromClause != null)
            {
                var conflicts = CollectConflictingAliases(querySpec.FromClause.TableReferences, cteNames);

                // Check for conflicts
                foreach (var (alias, tableRef) in conflicts)
                {
                    AddDiagnostic(
                        fragment: tableRef,
                        message: $"Table alias '{alias}' conflicts with a CTE name in the same query. Each name must be unique within the query scope.",
                        code: "semantic/cte-name-conflict",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }

        private static List<(string Alias, TableReference TableRef)> CollectConflictingAliases(
            IList<TableReference> tableRefs,
            HashSet<string> cteNames)
        {
            var result = new List<(string, TableReference)>();

            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    // Recursively collect from both sides of the JOIN
                    result.AddRange(CollectConflictingAliases(new[] { join.FirstTableReference }, cteNames));
                    result.AddRange(CollectConflictingAliases(new[] { join.SecondTableReference }, cteNames));
                }
                else if (tableRef is NamedTableReference namedTable)
                {
                    // Only flag conflict if there's an explicit alias that conflicts with a CTE
                    // If there's no alias and the table name is a CTE, that's a valid CTE reference
                    if (namedTable.Alias != null)
                    {
                        var aliasName = namedTable.Alias.Value;
                        if (cteNames.Contains(aliasName))
                        {
                            result.Add((aliasName, tableRef));
                        }
                    }
                    else
                    {
                        // No alias - check if table name conflicts with a CTE
                        // This would only be a conflict if it's NOT a CTE reference
                        var tableName = namedTable.SchemaObject.BaseIdentifier.Value;
                        // If the table name matches a CTE, it's a CTE reference, not a conflict
                        // Only flag if there's schema qualification (schema.table) that matches a CTE
                        if (namedTable.SchemaObject.SchemaIdentifier != null && cteNames.Contains(tableName))
                        {
                            result.Add((tableName, tableRef));
                        }
                    }
                }
                else if (tableRef is QueryDerivedTable derivedTable)
                {
                    // Subquery aliases can conflict with CTEs
                    var alias = derivedTable.Alias?.Value;
                    if (alias != null && cteNames.Contains(alias))
                    {
                        result.Add((alias, tableRef));
                    }
                }
            }

            return result;
        }
    }
}
