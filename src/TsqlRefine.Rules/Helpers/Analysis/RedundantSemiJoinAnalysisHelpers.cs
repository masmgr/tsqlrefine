using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Helpers.Analysis;

internal static class RedundantSemiJoinAnalysisHelpers
{
    public sealed record Match(BooleanExpression Predicate, InPredicate? InPredicate);

    public static IReadOnlyList<Match> FindMatches(QuerySpecification query)
    {
        if (query.FromClause?.TableReferences is not { Count: > 0 } tableReferences)
        {
            return [];
        }

        var joinFacts = new List<JoinFact>();
        foreach (var tableReference in tableReferences)
        {
            CollectInnerJoinFacts(tableReference, joinFacts);
        }

        if (joinFacts.Count == 0)
        {
            return [];
        }

        var collector = new PredicateCollector();
        query.WhereClause?.SearchCondition?.Accept(collector);
        query.HavingClause?.SearchCondition?.Accept(collector);

        var matches = new List<Match>();
        foreach (var predicate in collector.InPredicates)
        {
            if (TryMatchInPredicate(predicate, joinFacts))
            {
                matches.Add(new Match(predicate, predicate));
            }
        }

        foreach (var predicate in collector.ExistsPredicates)
        {
            if (TryMatchExistsPredicate(predicate, joinFacts))
            {
                matches.Add(new Match(predicate, null));
            }
        }

        return matches;
    }

    private static void CollectInnerJoinFacts(
        TableReference tableReference,
        ICollection<JoinFact> facts)
    {
        switch (tableReference)
        {
            case QualifiedJoin join:
                if (join.QualifiedJoinType == QualifiedJoinType.Inner)
                {
                    if (TryGetSingleNamedTable(join.SecondTableReference, out var secondTable))
                    {
                        AddJoinFacts(join, secondTable, facts);
                    }

                    if (TryGetSingleNamedTable(join.FirstTableReference, out var firstTable))
                    {
                        AddJoinFacts(join, firstTable, facts);
                    }
                }

                CollectInnerJoinFacts(join.FirstTableReference, facts);
                CollectInnerJoinFacts(join.SecondTableReference, facts);
                break;

            case JoinTableReference join:
                CollectInnerJoinFacts(join.FirstTableReference, facts);
                CollectInnerJoinFacts(join.SecondTableReference, facts);
                break;

            case JoinParenthesisTableReference parenthesis when parenthesis.Join is not null:
                CollectInnerJoinFacts(parenthesis.Join, facts);
                break;
        }
    }

    private static void AddJoinFacts(
        QualifiedJoin join,
        NamedTableReference joinedTable,
        ICollection<JoinFact> facts)
    {
        var joinedAlias = joinedTable.Alias?.Value ?? joinedTable.SchemaObject.BaseIdentifier?.Value;
        var identity = GetIdentity(joinedTable);
        if (joinedAlias is null || identity is null)
        {
            return;
        }

        foreach (var (left, right, _) in JoinEqualityPairCollector.Extract(join.SearchCondition))
        {
            if (!TryGetColumn(left, out var leftColumn) || !TryGetColumn(right, out var rightColumn))
            {
                continue;
            }

            if (leftColumn.Qualifier.Equals(joinedAlias, StringComparison.OrdinalIgnoreCase) &&
                !rightColumn.Qualifier.Equals(joinedAlias, StringComparison.OrdinalIgnoreCase))
            {
                facts.Add(new JoinFact(identity, leftColumn.ColumnName, rightColumn));
            }
            else if (rightColumn.Qualifier.Equals(joinedAlias, StringComparison.OrdinalIgnoreCase) &&
                     !leftColumn.Qualifier.Equals(joinedAlias, StringComparison.OrdinalIgnoreCase))
            {
                facts.Add(new JoinFact(identity, rightColumn.ColumnName, leftColumn));
            }
        }
    }

    private static bool TryMatchInPredicate(InPredicate predicate, IReadOnlyList<JoinFact> facts)
    {
        if (predicate.NotDefined ||
            predicate.Expression is not ColumnReferenceExpression outerColumnReference ||
            !TryGetColumn(outerColumnReference, out var outerColumn) ||
            !TryGetSimpleSubquery(predicate.Subquery, requireWhere: false, out var subquery, out var table, out var selectedColumn))
        {
            return false;
        }

        return facts.Any(fact =>
            fact.Table == table &&
            fact.JoinedColumn.Equals(selectedColumn, StringComparison.OrdinalIgnoreCase) &&
            fact.OuterColumn.Equals(outerColumn));
    }

    private static bool TryMatchExistsPredicate(ExistsPredicate predicate, IReadOnlyList<JoinFact> facts)
    {
        if (!TryGetSimpleSubquery(predicate.Subquery, requireWhere: true, out var subquery, out var table, out _))
        {
            return false;
        }

        if (subquery.WhereClause?.SearchCondition is not BooleanComparisonExpression
            {
                ComparisonType: BooleanComparisonType.Equals,
                FirstExpression: ColumnReferenceExpression left,
                SecondExpression: ColumnReferenceExpression right,
            })
        {
            return false;
        }

        if (!TryGetColumn(left, out var leftColumn) || !TryGetColumn(right, out var rightColumn))
        {
            return false;
        }

        var subqueryAlias = ((NamedTableReference)subquery.FromClause!.TableReferences[0]).Alias?.Value
            ?? ((NamedTableReference)subquery.FromClause.TableReferences[0]).SchemaObject.BaseIdentifier?.Value;
        if (subqueryAlias is null)
        {
            return false;
        }

        return facts.Any(fact =>
            fact.Table == table &&
            ((leftColumn.Qualifier.Equals(subqueryAlias, StringComparison.OrdinalIgnoreCase) &&
              leftColumn.ColumnName.Equals(fact.JoinedColumn, StringComparison.OrdinalIgnoreCase) &&
              rightColumn.Equals(fact.OuterColumn)) ||
             (rightColumn.Qualifier.Equals(subqueryAlias, StringComparison.OrdinalIgnoreCase) &&
              rightColumn.ColumnName.Equals(fact.JoinedColumn, StringComparison.OrdinalIgnoreCase) &&
              leftColumn.Equals(fact.OuterColumn))));
    }

    private static bool TryGetSimpleSubquery(
        ScalarSubquery? scalarSubquery,
        bool requireWhere,
        out QuerySpecification query,
        out TableIdentity table,
        out string selectedColumn)
    {
        query = null!;
        table = null!;
        selectedColumn = string.Empty;

        if (scalarSubquery?.QueryExpression is not QuerySpecification querySpecification ||
            querySpecification.TopRowFilter is not null ||
            querySpecification.UniqueRowFilter != UniqueRowFilter.NotSpecified ||
            querySpecification.GroupByClause is not null ||
            querySpecification.HavingClause is not null ||
            querySpecification.FromClause?.TableReferences is not [NamedTableReference namedTable] ||
            (!requireWhere && querySpecification.WhereClause is not null) ||
            (requireWhere && querySpecification.WhereClause is null))
        {
            return false;
        }

        var identity = GetIdentity(namedTable);
        if (identity is null)
        {
            return false;
        }

        if (querySpecification.SelectElements is [SelectScalarExpression { Expression: ColumnReferenceExpression selected }] &&
            TryGetColumn(selected, out var selectedReference) &&
            selectedReference.Qualifier.Equals(
                namedTable.Alias?.Value ?? namedTable.SchemaObject.BaseIdentifier?.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            selectedColumn = selectedReference.ColumnName;
        }
        else if (!requireWhere)
        {
            return false;
        }

        query = querySpecification;
        table = identity;
        return true;
    }

    private static bool TryGetSingleNamedTable(TableReference reference, out NamedTableReference table)
    {
        if (reference is NamedTableReference named)
        {
            table = named;
            return true;
        }

        table = null!;
        return false;
    }

    private static TableIdentity? GetIdentity(NamedTableReference table)
    {
        var identifiers = table.SchemaObject.Identifiers;
        return identifiers is { Count: > 0 }
            ? new TableIdentity(string.Join(".", identifiers.Select(identifier => identifier.Value)))
            : null;
    }

    private static bool TryGetColumn(ColumnReferenceExpression expression, out ColumnIdentity column)
    {
        var identifiers = expression.MultiPartIdentifier?.Identifiers;
        if (identifiers is not { Count: >= 2 })
        {
            column = default;
            return false;
        }

        column = new ColumnIdentity(identifiers[^2].Value, identifiers[^1].Value);
        return true;
    }

    private sealed class PredicateCollector : TSqlFragmentVisitor
    {
        public List<InPredicate> InPredicates { get; } = [];
        public List<ExistsPredicate> ExistsPredicates { get; } = [];

        public override void ExplicitVisit(BooleanNotExpression node)
        {
            // NOT IN and NOT EXISTS are never redundant merely because an INNER JOIN exists.
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            // Nested query scopes are analyzed when their QuerySpecification is visited.
        }

        public override void ExplicitVisit(InPredicate node)
        {
            InPredicates.Add(node);
            // Do not descend into the subquery.
        }

        public override void ExplicitVisit(ExistsPredicate node)
        {
            ExistsPredicates.Add(node);
            // Do not descend into the subquery.
        }
    }

    private sealed class TableIdentity(string name) : IEquatable<TableIdentity>
    {
        public string Name { get; } = name;

        public bool Equals(TableIdentity? other) =>
            other is not null && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is TableIdentity other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

        public static bool operator ==(TableIdentity? left, TableIdentity? right) => Equals(left, right);

        public static bool operator !=(TableIdentity? left, TableIdentity? right) => !Equals(left, right);
    }

    private readonly record struct ColumnIdentity(string Qualifier, string ColumnName)
    {
        public bool Equals(ColumnIdentity other) =>
            Qualifier.Equals(other.Qualifier, StringComparison.OrdinalIgnoreCase) &&
            ColumnName.Equals(other.ColumnName, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(Qualifier),
            StringComparer.OrdinalIgnoreCase.GetHashCode(ColumnName));
    }

    private sealed record JoinFact(TableIdentity Table, string JoinedColumn, ColumnIdentity OuterColumn);
}
