using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

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

                // Collect aliases and derived tables from FROM clause
                if (querySpec.FromClause != null)
                {
                    CollectFromClauseInfo(querySpec.FromClause.TableReferences, context);
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

            if (_queryStack.Count > 0)
            {
                var outerContext = _queryStack.Peek();

                // Get the set of aliases defined AFTER this derived table
                if (outerContext.DerivedTableLaterAliases.TryGetValue(node, out var laterAliases))
                {
                    // Get aliases referenced in this derived table using helper
                    var referencedAliases = CollectReferencedAliases(node);

                    // Check if any referenced aliases are from the outer query but defined AFTER this derived table
                    foreach (var alias in referencedAliases)
                    {
                        if (laterAliases.Contains(alias))
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
            }

            base.ExplicitVisit(node);
        }

        private static void CollectFromClauseInfo(IList<TableReference> tableRefs, QueryContext context)
        {
            // First pass: flatten all table references in order to track positions
            var flatRefs = new List<TableReference>();
            FlattenTableReferences(tableRefs, flatRefs);

            // Second pass: for each derived table, compute which aliases come AFTER it
            var allAliases = new List<(int Index, string Alias)>();
            var derivedTableIndices = new List<(int Index, QueryDerivedTable DerivedTable)>();

            for (var i = 0; i < flatRefs.Count; i++)
            {
                var tableRef = flatRefs[i];
                if (tableRef is QueryDerivedTable derivedTable)
                {
                    derivedTableIndices.Add((i, derivedTable));
                }
                else if (tableRef is NamedTableReference namedTable)
                {
                    var alias = namedTable.Alias?.Value ?? namedTable.SchemaObject.BaseIdentifier.Value;
                    allAliases.Add((i, alias));
                }
            }

            // For each derived table, "later aliases" are those with index > derived table index
            foreach (var (dtIndex, dt) in derivedTableIndices)
            {
                var laterAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (aliasIndex, alias) in allAliases)
                {
                    if (aliasIndex > dtIndex)
                    {
                        laterAliases.Add(alias);
                    }
                }

                context.DerivedTableLaterAliases[dt] = laterAliases;
            }
        }

        private static void FlattenTableReferences(IList<TableReference> tableRefs, List<TableReference> result)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    FlattenTableReferences([join.FirstTableReference], result);
                    FlattenTableReferences([join.SecondTableReference], result);
                }
                else
                {
                    result.Add(tableRef);
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

        /// <summary>
        /// Collects table qualifiers from column references, stopping at nested SELECT scope boundaries.
        /// Note: Does NOT stop at QueryDerivedTable since we need to traverse into the derived table
        /// we're analyzing - only stops at nested SELECT statements within.
        /// </summary>
        private sealed class AliasCollector : TSqlFragmentVisitor
        {
            private readonly HashSet<string> _aliases;

            public AliasCollector(HashSet<string> aliases)
            {
                _aliases = aliases;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                var qualifier = ColumnReferenceHelpers.GetTableQualifier(node);
                if (qualifier != null)
                {
                    _aliases.Add(qualifier);
                }

                base.ExplicitVisit(node);
            }

            // Stop at nested SELECT statements - they have their own scope
            public override void ExplicitVisit(SelectStatement node)
            {
                // Stop here - don't traverse into nested SELECT statements
            }
        }

        private sealed class QueryContext
        {
            // Maps each derived table to the set of aliases defined after it in the FROM clause
            public Dictionary<QueryDerivedTable, HashSet<string>> DerivedTableLaterAliases { get; } = [];
        }
    }
}
