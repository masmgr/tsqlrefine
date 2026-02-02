using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

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
        // Stack of alias sets for each query scope (innermost at top)
        private readonly Stack<HashSet<string>> _scopeStack = new();

        // Check if an alias is defined in any scope (current or outer)
        private bool IsAliasDefinedInAnyScope(string alias)
        {
            foreach (var scope in _scopeStack)
            {
                if (scope.Contains(alias))
                {
                    return true;
                }
            }

            return false;
        }

        private ScopeGuard PushScope(HashSet<string> aliases)
        {
            _scopeStack.Push(aliases);
            return new ScopeGuard(_scopeStack);
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            // Process CTEs first (they have their own independent scope)
            if (node.WithCtesAndXmlNamespaces?.CommonTableExpressions != null)
            {
                ProcessCteDefinitions(node.WithCtesAndXmlNamespaces.CommonTableExpressions);
            }

            // Then process the main query
            ProcessQueryExpression(node.QueryExpression);
        }

        private void ProcessCteDefinitions(IList<CommonTableExpression> ctes)
        {
            // Track available CTE names for chained/recursive CTE references
            var availableCteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cte in ctes)
            {
                var cteName = cte.ExpressionName?.Value;

                // Add current CTE name for self-reference (recursive CTEs)
                // Previous CTE names are also available (chained CTEs)
                if (cteName != null)
                {
                    availableCteNames.Add(cteName);
                }

                // Process CTE's query expression with available CTE names
                ProcessCteQueryExpression(cte.QueryExpression, availableCteNames);
            }
        }

        private void ProcessCteQueryExpression(QueryExpression? queryExpression, HashSet<string> availableCteNames)
        {
            switch (queryExpression)
            {
                case QuerySpecification querySpec:
                    ProcessCteQuerySpecification(querySpec, availableCteNames);
                    break;
                case BinaryQueryExpression binaryExpr:
                    // UNION ALL in recursive CTEs - both sides can reference the CTE
                    ProcessCteQueryExpression(binaryExpr.FirstQueryExpression, availableCteNames);
                    ProcessCteQueryExpression(binaryExpr.SecondQueryExpression, availableCteNames);
                    break;
                case QueryParenthesisExpression parenExpr:
                    ProcessCteQueryExpression(parenExpr.QueryExpression, availableCteNames);
                    break;
            }
        }

        private void ProcessCteQuerySpecification(QuerySpecification querySpec, HashSet<string> availableCteNames)
        {
            // Collect declared aliases from FROM clause
            var declaredAliases = querySpec.FromClause != null
                ? TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add available CTE names to the scope (for recursive/chained CTE references)
            foreach (var cteName in availableCteNames)
            {
                declaredAliases.Add(cteName);
            }

            // Check column references with scope management
            using (PushScope(declaredAliases))
            {
                var checker = new ColumnReferenceChecker(this);

                // Check SELECT list
                if (querySpec.SelectElements != null)
                {
                    foreach (var selectElement in querySpec.SelectElements)
                    {
                        selectElement.Accept(checker);
                    }
                }

                // Check WHERE clause
                querySpec.WhereClause?.Accept(checker);

                // Check GROUP BY clause
                querySpec.GroupByClause?.Accept(checker);

                // Check HAVING clause
                querySpec.HavingClause?.Accept(checker);

                // Check ORDER BY clause
                querySpec.OrderByClause?.Accept(checker);

                // Visit FROM clause for JOIN conditions and derived tables
                ProcessFromClause(querySpec.FromClause, checker);
            }
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            ProcessUpdateSpecification(node.UpdateSpecification);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            ProcessDeleteSpecification(node.DeleteSpecification);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            // Handle INSERT ... SELECT ... pattern
            if (node.InsertSpecification?.InsertSource is SelectInsertSource selectSource)
            {
                ProcessQueryExpression(selectSource.Select);
            }
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            // Scalar subqueries in SELECT, WHERE, etc. have their own scope
            ProcessQueryExpression(node.QueryExpression);
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            // Derived tables (subqueries in FROM clause) have their own scope
            ProcessQueryExpression(node.QueryExpression);
        }

        private void ProcessQueryExpression(QueryExpression? queryExpression)
        {
            switch (queryExpression)
            {
                case QuerySpecification querySpec:
                    ProcessQuerySpecification(querySpec);
                    break;
                case BinaryQueryExpression binaryExpr:
                    // UNION, INTERSECT, EXCEPT - validate both sides
                    ProcessQueryExpression(binaryExpr.FirstQueryExpression);
                    ProcessQueryExpression(binaryExpr.SecondQueryExpression);
                    break;
                case QueryParenthesisExpression parenExpr:
                    ProcessQueryExpression(parenExpr.QueryExpression);
                    break;
            }
        }

        private void ProcessQuerySpecification(QuerySpecification querySpec)
        {
            ProcessStatementWithFromClause(
                fromClause: querySpec.FromClause,
                target: null,
                visitClauses: checker =>
                {
                    // Check SELECT list
                    if (querySpec.SelectElements != null)
                    {
                        foreach (var selectElement in querySpec.SelectElements)
                        {
                            selectElement.Accept(checker);
                        }
                    }

                    // Check WHERE clause
                    querySpec.WhereClause?.Accept(checker);

                    // Check GROUP BY clause
                    querySpec.GroupByClause?.Accept(checker);

                    // Check HAVING clause
                    querySpec.HavingClause?.Accept(checker);

                    // Check ORDER BY clause
                    querySpec.OrderByClause?.Accept(checker);
                });
        }

        private void ProcessUpdateSpecification(UpdateSpecification updateSpec)
        {
            ProcessStatementWithFromClause(
                fromClause: updateSpec.FromClause,
                target: updateSpec.Target,
                visitClauses: checker =>
                {
                    // Check SET clauses
                    foreach (var setClause in updateSpec.SetClauses)
                    {
                        setClause.Accept(checker);
                    }

                    // Check WHERE clause
                    updateSpec.WhereClause?.Accept(checker);
                });
        }

        private void ProcessDeleteSpecification(DeleteSpecification deleteSpec)
        {
            ProcessStatementWithFromClause(
                fromClause: deleteSpec.FromClause,
                target: deleteSpec.Target,
                visitClauses: checker =>
                {
                    // Check WHERE clause
                    deleteSpec.WhereClause?.Accept(checker);
                });
        }

        private void ProcessStatementWithFromClause(
            FromClause? fromClause,
            TableReference? target,
            Action<ColumnReferenceChecker> visitClauses)
        {
            // Phase 1: Collect declared aliases from FROM clause
            var declaredAliases = fromClause != null
                ? TableReferenceHelpers.CollectTableAliases(fromClause.TableReferences)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add target table alias for UPDATE/DELETE
            if (target != null)
            {
                var targetAlias = TableReferenceHelpers.GetAliasOrTableName(target);
                if (targetAlias != null)
                {
                    declaredAliases.Add(targetAlias);
                }
            }

            // Phase 2: Check column references with scope management
            using (PushScope(declaredAliases))
            {
                var checker = new ColumnReferenceChecker(this);

                // Visit statement-specific clauses
                visitClauses(checker);

                // Visit FROM clause for JOIN conditions and derived tables
                ProcessFromClause(fromClause, checker);
            }
        }

        private void ProcessFromClause(FromClause? fromClause, ColumnReferenceChecker checker)
        {
            if (fromClause == null)
            {
                return;
            }

            foreach (var tableRef in fromClause.TableReferences)
            {
                // Check JOIN conditions using existing helper
                TableReferenceHelpers.TraverseJoinConditions(tableRef, (_, condition) =>
                    condition.Accept(checker));

                // Process derived tables (subqueries in FROM) with their own scope
                TraverseDerivedTables(tableRef);
            }
        }

        private void TraverseDerivedTables(TableReference tableRef)
        {
            if (tableRef is QueryDerivedTable derivedTable)
            {
                ProcessQueryExpression(derivedTable.QueryExpression);
            }
            else if (tableRef is JoinTableReference join)
            {
                TraverseDerivedTables(join.FirstTableReference);
                TraverseDerivedTables(join.SecondTableReference);
            }
        }

        private sealed class ColumnReferenceChecker : ScopeDelegatingVisitor
        {
            private readonly UndefinedAliasVisitor _parent;

            public ColumnReferenceChecker(UndefinedAliasVisitor parent)
            {
                _parent = parent;
            }

            protected override void ProcessSubquery(QueryExpression? queryExpression)
            {
                _parent.ProcessQueryExpression(queryExpression);
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                // Check if this is a qualified column reference (e.g., table.column)
                var qualifier = ColumnReferenceHelpers.GetTableQualifier(node);

                // Check against ALL scopes (current + outer) to support correlated subqueries
                if (qualifier != null && !_parent.IsAliasDefinedInAnyScope(qualifier))
                {
                    var identifiers = node.MultiPartIdentifier!.Identifiers;
                    var count = identifiers.Count;

                    // Multi-part identifier patterns:
                    // 2 parts: table.column                   -> qualifier at index 0
                    // 3 parts: schema.table.column            -> qualifier at index 1
                    // 4 parts: server.schema.table.column     -> qualifier at index 2
                    // Formula: qualifier index = count - 2
                    var qualifierIndex = count - 2;

                    _parent.AddDiagnostic(
                        fragment: identifiers[qualifierIndex],
                        message: $"Undefined table alias '{qualifier}'. Table or alias '{qualifier}' is not declared in the FROM clause.",
                        code: "semantic/undefined-alias",
                        category: "Correctness",
                        fixable: false
                    );
                }

                base.ExplicitVisit(node);
            }
        }

        private readonly struct ScopeGuard(Stack<HashSet<string>> stack) : IDisposable
        {
            public void Dispose() => stack.Pop();
        }
    }
}
