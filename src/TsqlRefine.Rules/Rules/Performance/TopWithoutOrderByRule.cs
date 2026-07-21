using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects TOP clause without ORDER BY, which produces non-deterministic results.
/// When schema information is available, suppresses the diagnostic if the WHERE clause
/// filters on a unique column set (PK, unique constraint, or unique index), guaranteeing
/// at most one row regardless of ordering.
/// </summary>
public sealed class TopWithoutOrderByRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "top-without-order-by",
        Description: "Detects TOP clause without ORDER BY, which produces non-deterministic results.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new TopWithoutOrderByVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "The visitor combines existing TOP, query-shape, and schema analyses; this rule-specific coupling is intentional.")]
    private sealed class TopWithoutOrderByVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        private readonly HashSet<QuerySpecification> _selectIntoQueries = [];
        private int _existsDepth;

        public override void ExplicitVisit(ExistsPredicate node)
        {
            _existsDepth++;
            base.ExplicitVisit(node);
            _existsDepth--;
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var added = QueryExpressionAnalysisHelpers.EnumerateQuerySpecifications(node.QueryExpression)
                .Where(_selectIntoQueries.Add)
                .ToArray();
            base.ExplicitVisit(node);
            foreach (var query in added)
            {
                _selectIntoQueries.Remove(query);
            }
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (_existsDepth == 0 &&
                !_selectIntoQueries.Contains(node) &&
                node.TopRowFilter != null &&
                node.OrderByClause == null &&
                !IsTopZero(node.TopRowFilter.Expression) &&
                !IsTopOneHundredPercent(node.TopRowFilter) &&
                !IsSingleRowAggregateQuery(node))
            {
                if (!IsWhereOnUniqueColumns(node))
                {
                    AddDiagnostic(
                        fragment: node.TopRowFilter,
                        message: "TOP clause without ORDER BY produces non-deterministic results. Add an ORDER BY clause to ensure consistent results.",
                        code: "top-without-order-by",
                        category: "Performance",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks whether the WHERE clause filters on a complete unique column set for any table.
        /// </summary>
        private bool IsWhereOnUniqueColumns(QuerySpecification node)
        {
            if (schema is null ||
                node.WhereClause?.SearchCondition is null ||
                node.FromClause?.TableReferences is not { Count: > 0 } tableRefs)
            {
                return false;
            }

            // Suppress only for simple single-table queries.
            // In joined queries, uniqueness on one side does not guarantee a single-row result set.
            if (tableRefs.Count != 1 || tableRefs[0] is not NamedTableReference)
            {
                return false;
            }

            var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            var equalityColumns = CollectEqualityColumns(node.WhereClause.SearchCondition);

            if (equalityColumns.Count == 0)
            {
                return false;
            }

            if (aliasMap.AllTables.Count != 1)
            {
                return false;
            }

            var resolvedTable = aliasMap.AllTables[0];
            var columnsForTable = GetColumnsForTable(equalityColumns, resolvedTable, aliasMap);
            return columnsForTable.Count > 0 &&
                   schema.IsUniqueColumnSet(resolvedTable, columnsForTable);
        }

        /// <summary>
        /// Collects column references from AND-connected equality conditions in the WHERE clause.
        /// OR branches are conservatively ignored (cannot guarantee uniqueness).
        /// </summary>
        private static List<(string? Qualifier, string ColumnName)> CollectEqualityColumns(
            BooleanExpression condition)
        {
            var result = new List<(string?, string)>();
            CollectEqualityColumnsCore(condition, result);
            return result;
        }

        private static void CollectEqualityColumnsCore(
            BooleanExpression expression,
            List<(string? Qualifier, string ColumnName)> result)
        {
            switch (expression)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comp:
                    AddColumnFromComparison(comp.FirstExpression, result);
                    AddColumnFromComparison(comp.SecondExpression, result);
                    break;

                case BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary:
                    CollectEqualityColumnsCore(binary.FirstExpression, result);
                    CollectEqualityColumnsCore(binary.SecondExpression, result);
                    break;

                case BooleanParenthesisExpression paren:
                    CollectEqualityColumnsCore(paren.Expression, result);
                    break;

                    // OR, NOT, and other expressions are conservatively ignored
            }
        }

        private static void AddColumnFromComparison(
            ScalarExpression expression,
            List<(string? Qualifier, string ColumnName)> result)
        {
            if (expression is ColumnReferenceExpression colRef &&
                colRef.MultiPartIdentifier?.Identifiers is { Count: > 0 } identifiers)
            {
                var columnName = identifiers[^1].Value;
                var qualifier = ColumnReferenceHelpers.GetTableQualifier(colRef);
                result.Add((qualifier, columnName));
            }
        }

        /// <summary>
        /// Filters equality columns to those belonging to a specific resolved table.
        /// </summary>
        private static List<string> GetColumnsForTable(
            List<(string? Qualifier, string ColumnName)> equalityColumns,
            ResolvedTable resolvedTable,
            AliasMap aliasMap)
        {
            var columns = new List<string>();

            foreach (var (qualifier, columnName) in equalityColumns)
            {
                if (qualifier is null)
                {
                    // Unqualified column — if only one table in scope, assume it belongs there
                    if (aliasMap.AllTables.Count == 1)
                    {
                        columns.Add(columnName);
                    }
                }
                else if (aliasMap.TryResolve(qualifier, out var resolved) &&
                         resolved == resolvedTable)
                {
                    columns.Add(columnName);
                }
            }

            return columns;
        }

        private static bool IsTopZero(ScalarExpression expression) =>
            expression is IntegerLiteral lit && lit.Value == "0" ||
            expression is ParenthesisExpression paren && IsTopZero(paren.Expression);

        private static bool IsTopOneHundredPercent(TopRowFilter topRowFilter) =>
            topRowFilter.Percent &&
            TryGetLiteralValue(topRowFilter.Expression, out var value) &&
            value == 100m;

        private static bool TryGetLiteralValue(ScalarExpression expression, out decimal value)
        {
            if (expression is ParenthesisExpression parenthesis)
            {
                return TryGetLiteralValue(parenthesis.Expression, out value);
            }

            if (expression is not Literal literal)
            {
                value = default;
                return false;
            }

            return decimal.TryParse(literal.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool IsSingleRowAggregateQuery(QuerySpecification node)
        {
            if (node.GroupByClause is not null || node.SelectElements.Count == 0)
            {
                return false;
            }

            return node.SelectElements.All(selectElement => selectElement switch
            {
                SelectScalarExpression { Expression: FunctionCall function } => IsNonWindowAggregate(function),
                SelectSetVariable { Expression: FunctionCall function } => IsNonWindowAggregate(function),
                _ => false
            });
        }

        private static bool IsNonWindowAggregate(FunctionCall function) =>
            function.OverClause is null && TsqlRefine.Rules.Helpers.Analysis.AggregateFunctionHelpers.IsAggregateFunction(function);

    }
}
