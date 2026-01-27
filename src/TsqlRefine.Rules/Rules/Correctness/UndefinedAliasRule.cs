using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class UndefinedAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/undefined-alias",
        Description: "Detects references to undefined table aliases in column qualifiers.",
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

        var visitor = new UndefinedAliasVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UndefinedAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;
            if (querySpec == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Phase 1: Collect declared aliases from FROM clause
            var declaredAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (querySpec.FromClause != null)
            {
                CollectDeclaredAliases(querySpec.FromClause.TableReferences, declaredAliases);
            }

            // Phase 2: Check column references in the entire query
            var columnRefChecker = new ColumnReferenceChecker(declaredAliases, this);

            // Check SELECT list
            if (querySpec.SelectElements != null)
            {
                foreach (var selectElement in querySpec.SelectElements)
                {
                    selectElement.Accept(columnRefChecker);
                }
            }

            // Check WHERE clause
            querySpec.WhereClause?.Accept(columnRefChecker);

            // Check GROUP BY clause
            querySpec.GroupByClause?.Accept(columnRefChecker);

            // Check HAVING clause
            querySpec.HavingClause?.Accept(columnRefChecker);

            // Check ORDER BY clause
            querySpec.OrderByClause?.Accept(columnRefChecker);

            // Check JOIN conditions (these are part of FROM clause)
            if (querySpec.FromClause != null)
            {
                foreach (var tableRef in querySpec.FromClause.TableReferences)
                {
                    CheckJoinConditions(tableRef, columnRefChecker);
                }
            }

            base.ExplicitVisit(node);
        }

        private static void CollectDeclaredAliases(IList<TableReference> tableRefs, HashSet<string> declaredAliases)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    // Recursively collect from both sides of the JOIN
                    var leftRefs = new List<TableReference> { join.FirstTableReference };
                    CollectDeclaredAliases(leftRefs, declaredAliases);

                    var rightRefs = new List<TableReference> { join.SecondTableReference };
                    CollectDeclaredAliases(rightRefs, declaredAliases);
                }
                else
                {
                    var alias = GetAliasOrTableName(tableRef);
                    if (alias != null)
                    {
                        declaredAliases.Add(alias);
                    }
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

        private static void CheckJoinConditions(TableReference tableRef, ColumnReferenceChecker checker)
        {
            if (tableRef is QualifiedJoin qualifiedJoin)
            {
                qualifiedJoin.SearchCondition?.Accept(checker);
                CheckJoinConditions(qualifiedJoin.FirstTableReference, checker);
                CheckJoinConditions(qualifiedJoin.SecondTableReference, checker);
            }
            else if (tableRef is JoinTableReference join)
            {
                CheckJoinConditions(join.FirstTableReference, checker);
                CheckJoinConditions(join.SecondTableReference, checker);
            }
        }

        private sealed class ColumnReferenceChecker : TSqlFragmentVisitor
        {
            private readonly HashSet<string> _declaredAliases;
            private readonly UndefinedAliasVisitor _parent;

            public ColumnReferenceChecker(HashSet<string> declaredAliases, UndefinedAliasVisitor parent)
            {
                _declaredAliases = declaredAliases;
                _parent = parent;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                // Check if this is a qualified column reference (e.g., table.column)
                if (node.MultiPartIdentifier?.Identifiers?.Count > 1)
                {
                    var qualifier = node.MultiPartIdentifier.Identifiers[0].Value;

                    if (!_declaredAliases.Contains(qualifier))
                    {
                        _parent.AddDiagnostic(
                            fragment: node.MultiPartIdentifier.Identifiers[0],
                            message: $"Undefined table alias '{qualifier}'. Table or alias '{qualifier}' is not declared in the FROM clause.",
                            code: "semantic/undefined-alias",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                }

                base.ExplicitVisit(node);
            }

            // Don't descend into subqueries - they have their own scope
            public override void ExplicitVisit(SelectStatement node)
            {
                // Stop here - don't traverse into nested SELECT statements
                // The parent visitor will handle them separately
            }
        }
    }
}
