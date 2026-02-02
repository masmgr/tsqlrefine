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

        public override void ExplicitVisit(SelectStatement node)
        {
            ProcessQueryExpression(node.QueryExpression);
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
            // Phase 1: Collect declared aliases from FROM clause
            var declaredAliases = querySpec.FromClause != null
                ? TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Push this scope onto the stack
            _scopeStack.Push(declaredAliases);

            try
            {
                // Phase 2: Check column references in the query
                var columnRefChecker = new ColumnReferenceChecker(this);

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

                // Check JOIN conditions and process nested subqueries in FROM clause
                if (querySpec.FromClause != null)
                {
                    foreach (var tableRef in querySpec.FromClause.TableReferences)
                    {
                        ProcessTableReference(tableRef, columnRefChecker);
                    }
                }
            }
            finally
            {
                // Pop this scope
                _scopeStack.Pop();
            }
        }

        private void ProcessTableReference(TableReference tableRef, ColumnReferenceChecker checker)
        {
            if (tableRef is QualifiedJoin qualifiedJoin)
            {
                qualifiedJoin.SearchCondition?.Accept(checker);
                ProcessTableReference(qualifiedJoin.FirstTableReference, checker);
                ProcessTableReference(qualifiedJoin.SecondTableReference, checker);
            }
            else if (tableRef is JoinTableReference join)
            {
                ProcessTableReference(join.FirstTableReference, checker);
                ProcessTableReference(join.SecondTableReference, checker);
            }
            else if (tableRef is QueryDerivedTable derivedTable)
            {
                // Process the derived table's inner query with its own scope
                ProcessQueryExpression(derivedTable.QueryExpression);
            }
        }

        private sealed class ColumnReferenceChecker : TSqlFragmentVisitor
        {
            private readonly UndefinedAliasVisitor _parent;

            public ColumnReferenceChecker(UndefinedAliasVisitor parent)
            {
                _parent = parent;
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

            // Don't descend into subqueries - they have their own scope
            // Instead, delegate to parent visitor to process them with proper scope tracking
            public override void ExplicitVisit(SelectStatement node)
            {
                // Process the nested SELECT with its own scope
                _parent.ProcessQueryExpression(node.QueryExpression);
            }

            public override void ExplicitVisit(ScalarSubquery node)
            {
                // Process the scalar subquery with its own scope
                _parent.ProcessQueryExpression(node.QueryExpression);
            }

            public override void ExplicitVisit(QueryDerivedTable node)
            {
                // Process the derived table with its own scope
                _parent.ProcessQueryExpression(node.QueryExpression);
            }
        }
    }
}
