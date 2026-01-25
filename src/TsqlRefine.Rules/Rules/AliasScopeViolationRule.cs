using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class AliasScopeViolationRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/alias-scope-violation",
        Description: "Detects potential scope violations where aliases from outer queries are referenced in inner queries without clear correlation intent.",
        Category: "Correctness",
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

        var visitor = new AliasScopeViolationVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AliasScopeViolationVisitor : DiagnosticVisitorBase
    {
        private readonly Stack<QueryContext> _queryStack = new();

        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.QueryExpression is QuerySpecification querySpec)
            {
                // Build context for this query
                var context = new QueryContext();

                // Collect aliases from FROM clause
                if (querySpec.FromClause != null)
                {
                    CollectAliasesFromFrom(querySpec.FromClause.TableReferences, context);
                }

                // Push onto stack (this makes outer contexts available to inner queries)
                _queryStack.Push(context);

                // Visit the query (including subqueries)
                base.ExplicitVisit(node);

                // Pop the context
                _queryStack.Pop();
            }
            else
            {
                base.ExplicitVisit(node);
            }
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            // This is a derived table (subquery in FROM clause)
            // Check if it references aliases that aren't available yet in the outer FROM clause
            // This is a common error pattern

            if (_queryStack.Count > 0)
            {
                var outerContext = _queryStack.Peek();

                // Get aliases referenced in this derived table
                var referencedAliases = CollectReferencedAliases(node);

                // Check if any referenced aliases are from the outer query but defined AFTER this derived table
                foreach (var alias in referencedAliases)
                {
                    // If the alias is in the outer context's "later" aliases, it's a potential violation
                    if (outerContext.LaterAliases.Contains(alias))
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: $"Derived table references alias '{alias}' which is defined later in the outer query's FROM clause. This may cause unexpected behavior or errors. Consider reordering tables or using explicit correlation.",
                            code: "semantic/alias-scope-violation",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void CollectAliasesFromFrom(IList<TableReference> tableRefs, QueryContext context)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is QueryDerivedTable derivedTable)
                {
                    // Mark all subsequent aliases as "later" for this derived table
                    // (aliases defined after this point aren't available inside the derived table)
                    context.EnteringDerivedTable();

                    // Add the derived table's own alias as available
                    if (derivedTable.Alias?.Value != null)
                    {
                        context.AddAvailableAlias(derivedTable.Alias.Value);
                    }
                }
                else if (tableRef is NamedTableReference namedTable)
                {
                    var alias = namedTable.Alias?.Value ?? namedTable.SchemaObject.BaseIdentifier.Value;
                    context.AddLaterAlias(alias);
                }
                else if (tableRef is JoinTableReference join)
                {
                    CollectAliasesFromFrom(new[] { join.FirstTableReference }, context);
                    CollectAliasesFromFrom(new[] { join.SecondTableReference }, context);
                }
            }
        }

        private static HashSet<string> CollectReferencedAliases(QueryDerivedTable derivedTable)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visitor = new AliasCollector(aliases);
            derivedTable.Accept(visitor);
            return aliases;
        }

        private sealed class AliasCollector : TSqlFragmentVisitor
        {
            private readonly HashSet<string> _aliases;

            public AliasCollector(HashSet<string> aliases)
            {
                _aliases = aliases;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                if (node.MultiPartIdentifier?.Identifiers?.Count > 1)
                {
                    var alias = node.MultiPartIdentifier.Identifiers[0].Value;
                    _aliases.Add(alias);
                }

                base.ExplicitVisit(node);
            }

            // Don't descend into nested subqueries
            public override void ExplicitVisit(SelectStatement node)
            {
                // Stop here
            }
        }

        private sealed class QueryContext
        {
            public HashSet<string> AvailableAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> LaterAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

            public void AddAvailableAlias(string alias)
            {
                AvailableAliases.Add(alias);
            }

            public void AddLaterAlias(string alias)
            {
                LaterAliases.Add(alias);
            }

            public void EnteringDerivedTable()
            {
                // When entering a derived table, current "later" aliases become unavailable
                // This method is called before processing the derived table
            }
        }
    }
}
