using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects references to undefined table aliases in column qualifiers.
/// </summary>
public sealed class UndefinedAliasRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/undefined-alias",
        Description: "Detects references to undefined table aliases in column qualifiers.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UndefinedAliasVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UndefinedAliasVisitor : DiagnosticVisitorBase
    {
        private readonly AliasScopeManager _scopeManager = new();
        private readonly ColumnReferenceChecker _checker;

        public UndefinedAliasVisitor()
        {
            _checker = new ColumnReferenceChecker(this);
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            ProcessCommonTableExpressions(node.WithCtesAndXmlNamespaces);

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
                ProcessQueryExpression(cte.QueryExpression, availableCteNames);
            }
        }

        private void ProcessCommonTableExpressions(WithCtesAndXmlNamespaces? withCtesAndXmlNamespaces)
        {
            if (withCtesAndXmlNamespaces?.CommonTableExpressions != null)
            {
                ProcessCteDefinitions(withCtesAndXmlNamespaces.CommonTableExpressions);
            }
        }

        private void ProcessQueryExpression(QueryExpression? queryExpression, HashSet<string>? cteNames = null)
        {
            switch (queryExpression)
            {
                case QuerySpecification querySpec:
                    ProcessQuerySpecification(querySpec, cteNames);
                    break;
                case BinaryQueryExpression binaryExpr:
                    // UNION, INTERSECT, EXCEPT - validate both sides
                    ProcessQueryExpression(binaryExpr.FirstQueryExpression, cteNames);
                    ProcessQueryExpression(binaryExpr.SecondQueryExpression, cteNames);
                    break;
                case QueryParenthesisExpression parenExpr:
                    ProcessQueryExpression(parenExpr.QueryExpression, cteNames);
                    break;
            }
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            ProcessCommonTableExpressions(node.WithCtesAndXmlNamespaces);
            ProcessUpdateSpecification(node.UpdateSpecification);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            ProcessCommonTableExpressions(node.WithCtesAndXmlNamespaces);
            ProcessDeleteSpecification(node.DeleteSpecification);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            ProcessCommonTableExpressions(node.WithCtesAndXmlNamespaces);
            ProcessInsertSpecification(node.InsertSpecification);
        }

        public override void ExplicitVisit(MergeStatement node)
        {
            ProcessCommonTableExpressions(node.WithCtesAndXmlNamespaces);
            ProcessMergeSpecification(node.MergeSpecification);
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

        private void ProcessQuerySpecification(QuerySpecification querySpec, HashSet<string>? cteNames = null)
        {
            var declaredAliases = CollectScopeAliases(querySpec.FromClause, null, cteNames);

            using (_scopeManager.PushScope(declaredAliases))
            {
                ValidateSelectClauses(querySpec);
                ProcessFromClause(querySpec.FromClause);
            }
        }

        private void ProcessUpdateSpecification(UpdateSpecification updateSpec)
        {
            var declaredAliases = CollectScopeAliases(updateSpec.FromClause, updateSpec.Target, null);

            using (_scopeManager.PushScope(declaredAliases))
            {
                // Check SET clauses
                foreach (var setClause in updateSpec.SetClauses)
                {
                    setClause.Accept(_checker);
                }

                // Check WHERE clause
                updateSpec.WhereClause?.Accept(_checker);

                ProcessOutputClauses(
                    updateSpec.OutputClause,
                    updateSpec.OutputIntoClause,
                    ["inserted", "deleted"]);

                // Visit FROM clause including JOIN/APPLY expressions and derived tables
                ProcessFromClause(updateSpec.FromClause);
            }
        }

        private void ProcessDeleteSpecification(DeleteSpecification deleteSpec)
        {
            var declaredAliases = CollectScopeAliases(deleteSpec.FromClause, deleteSpec.Target, null);

            using (_scopeManager.PushScope(declaredAliases))
            {
                // Check WHERE clause
                deleteSpec.WhereClause?.Accept(_checker);

                ProcessOutputClauses(
                    deleteSpec.OutputClause,
                    deleteSpec.OutputIntoClause,
                    ["deleted"]);

                // Visit FROM clause including JOIN/APPLY expressions and derived tables
                ProcessFromClause(deleteSpec.FromClause);
            }
        }

        private void ProcessInsertSpecification(InsertSpecification? insertSpec)
        {
            if (insertSpec == null)
            {
                return;
            }

            // Handle INSERT ... SELECT ... pattern
            if (insertSpec.InsertSource is SelectInsertSource selectSource)
            {
                ProcessQueryExpression(selectSource.Select);
            }

            ProcessOutputClauses(
                insertSpec.OutputClause,
                insertSpec.OutputIntoClause,
                ["inserted"]);
        }

        private void ProcessMergeSpecification(MergeSpecification? mergeSpec)
        {
            if (mergeSpec == null)
            {
                return;
            }

            var declaredAliases = CollectMergeScopeAliases(mergeSpec);

            using (_scopeManager.PushScope(declaredAliases))
            {
                // Check ON condition.
                mergeSpec.SearchCondition?.Accept(_checker);

                // Check action clauses.
                foreach (var actionClause in mergeSpec.ActionClauses ?? [])
                {
                    actionClause.Accept(_checker);
                }

                // Validate source table reference including APPLY arguments.
                ProcessTableReference(mergeSpec.TableReference);

                ProcessOutputClauses(
                    mergeSpec.OutputClause,
                    mergeSpec.OutputIntoClause,
                    ["inserted", "deleted"]);
            }
        }

        private static HashSet<string> CollectMergeScopeAliases(MergeSpecification mergeSpec)
        {
            var declaredAliases = mergeSpec.TableReference != null
                ? TableReferenceHelpers.CollectTableAliases([mergeSpec.TableReference])
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // MERGE target alias is represented separately from Target table reference.
            if (!string.IsNullOrWhiteSpace(mergeSpec.TableAlias?.Value))
            {
                declaredAliases.Add(mergeSpec.TableAlias.Value);
            }
            else if (mergeSpec.Target != null)
            {
                var targetAliasOrTableName = TableReferenceHelpers.GetAliasOrTableName(mergeSpec.Target);
                if (targetAliasOrTableName != null)
                {
                    declaredAliases.Add(targetAliasOrTableName);
                }
            }

            return declaredAliases;
        }

        private void ProcessOutputClauses(
            OutputClause? outputClause,
            OutputIntoClause? outputIntoClause,
            string[] pseudoAliases)
        {
            if (outputClause == null && outputIntoClause == null)
            {
                return;
            }

            using (_scopeManager.PushScope(CreateAliasSet(pseudoAliases)))
            {
                outputClause?.Accept(_checker);
                outputIntoClause?.Accept(_checker);
            }
        }

        private static HashSet<string> CreateAliasSet(string[] aliases)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var alias in aliases)
            {
                result.Add(alias);
            }

            return result;
        }

        private static HashSet<string> CollectScopeAliases(
            FromClause? fromClause,
            TableReference? target,
            HashSet<string>? cteNames)
        {
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

            // Add available CTE names to the scope (for recursive/chained CTE references)
            if (cteNames != null)
            {
                foreach (var cteName in cteNames)
                {
                    declaredAliases.Add(cteName);
                }
            }

            return declaredAliases;
        }

        private void ValidateSelectClauses(QuerySpecification querySpec)
        {
            // Check SELECT list
            foreach (var selectElement in querySpec.SelectElements ?? [])
            {
                selectElement.Accept(_checker);
            }

            // Check WHERE, GROUP BY, HAVING, ORDER BY clauses
            querySpec.WhereClause?.Accept(_checker);
            querySpec.GroupByClause?.Accept(_checker);
            querySpec.HavingClause?.Accept(_checker);
            querySpec.OrderByClause?.Accept(_checker);
        }

        private void ProcessFromClause(FromClause? fromClause)
        {
            if (fromClause == null)
            {
                return;
            }

            foreach (var tableRef in fromClause.TableReferences)
            {
                // Process full table reference tree (JOIN/APPLY arguments, derived tables, etc.)
                ProcessTableReference(tableRef);
            }
        }

        private void ProcessTableReference(TableReference? tableRef)
        {
            tableRef?.Accept(_checker);
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
                var qualifierInfo = ColumnReferenceHelpers.GetTableQualifierWithIdentifier(node);

                // Check against ALL scopes (current + outer) to support correlated subqueries
                if (qualifierInfo.HasValue && !_parent._scopeManager.IsAliasDefinedInAnyScope(qualifierInfo.Value.Qualifier))
                {
                    var (qualifier, qualifierIdentifier) = qualifierInfo.Value;

                    _parent.AddDiagnostic(
                        fragment: qualifierIdentifier,
                        message: $"Undefined table alias '{qualifier}'. Table or alias '{qualifier}' is not declared in the FROM clause.",
                        code: "semantic/undefined-alias",
                        category: "Correctness",
                        fixable: false
                    );
                }

                base.ExplicitVisit(node);
            }
        }
    }
}
