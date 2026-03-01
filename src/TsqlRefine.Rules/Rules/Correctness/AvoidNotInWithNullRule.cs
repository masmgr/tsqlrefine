using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values.
/// When schema information is available, suppresses the warning if the subquery's SELECT column is NOT NULL.
/// </summary>
public sealed class AvoidNotInWithNullRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-not-in-with-null",
        Description: "Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidNotInWithNullVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidNotInWithNullVisitor(ISchemaProvider? schema) : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(InPredicate node)
        {
            if (IsInPredicate && node.NotDefined && node.Subquery is not null)
            {
                if (!IsSubqueryColumnNotNullable(node.Subquery))
                {
                    AddDiagnostic(
                        range: ScriptDomHelpers.GetInKeywordRange(node),
                        message: "NOT IN with subquery can produce unexpected empty results when the subquery returns NULL values. Use NOT EXISTS or EXCEPT instead.",
                        code: "avoid-not-in-with-null",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Determines if the subquery's single SELECT column is provably NOT NULL
        /// using schema information. Returns true only when definitively not nullable.
        /// </summary>
        private bool IsSubqueryColumnNotNullable(ScalarSubquery subquery)
        {
            if (schema is null)
            {
                return false;
            }

            if (subquery.QueryExpression is not QuerySpecification querySpec)
            {
                return false;
            }

            if (querySpec.SelectElements is not [SelectScalarExpression { Expression: ColumnReferenceExpression colRef }])
            {
                return false;
            }

            if (colRef.ColumnType == ColumnType.Wildcard)
            {
                return false;
            }

            if (querySpec.FromClause?.TableReferences is not { Count: > 0 } tableRefs)
            {
                return false;
            }

            var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            var resolved = ResolveColumn(colRef, aliasMap);

            if (resolved is not { Column.IsNullable: false })
            {
                return false;
            }

            var columnQualifier = ColumnReferenceHelpers.GetTableQualifier(colRef);
            return !HasNullIntroducingJoinForColumnSource(
                tableRefs,
                aliasMap,
                resolved.Table,
                columnQualifier);
        }

        private ResolvedColumn? ResolveColumn(ColumnReferenceExpression colRef, AliasMap aliasMap)
        {
            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return null;
            }

            if (identifiers.Count >= 2)
            {
                var columnName = identifiers[^1].Value;

                if (!TryResolveQualifiedTable(identifiers, aliasMap, out var resolvedTable)
                    || resolvedTable is null)
                {
                    return null;
                }

                return schema!.ResolveColumn(resolvedTable, columnName);
            }

            // Unqualified column — search all tables in the alias map
            var unqualifiedName = identifiers[0].Value;
            foreach (var table in aliasMap.AllTables)
            {
                var resolved = schema!.ResolveColumn(table, unqualifiedName);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static bool TryResolveQualifiedTable(
            IList<Identifier> identifiers,
            AliasMap aliasMap,
            out ResolvedTable? resolvedTable)
        {
            foreach (var key in BuildQualifierLookupKeys(identifiers))
            {
                if (aliasMap.TryResolve(key, out resolvedTable))
                {
                    return true;
                }
            }

            resolvedTable = null;
            return false;
        }

        private static IEnumerable<string> BuildQualifierLookupKeys(IList<Identifier> identifiers)
        {
            var qualifierCount = identifiers.Count - 1;
            if (qualifierCount <= 0)
            {
                yield break;
            }

            var parts = new string[qualifierCount];
            for (var i = 0; i < qualifierCount; i++)
            {
                parts[i] = identifiers[i].Value;
            }

            if (parts.Length == 1)
            {
                yield return parts[0];
                yield break;
            }

            yield return string.Join(".", parts);

            if (parts.Length >= 2)
            {
                yield return $"{parts[^2]}.{parts[^1]}";
            }

            yield return parts[^1];
        }

        private static bool HasNullIntroducingJoinForColumnSource(
            IList<TableReference> tableRefs,
            AliasMap aliasMap,
            ResolvedTable columnTable,
            string? qualifier)
        {
            foreach (var tableRef in tableRefs)
            {
                if (HasNullIntroducingJoinForColumnSource(
                    tableRef,
                    aliasMap,
                    columnTable,
                    qualifier))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNullIntroducingJoinForColumnSource(
            TableReference tableRef,
            AliasMap aliasMap,
            ResolvedTable columnTable,
            string? qualifier)
        {
            switch (tableRef)
            {
                case QualifiedJoin qualifiedJoin:
                    if (IsNullIntroducingForColumnSource(qualifiedJoin, aliasMap, columnTable, qualifier))
                    {
                        return true;
                    }

                    return HasNullIntroducingJoinForColumnSource(
                               qualifiedJoin.FirstTableReference,
                               aliasMap,
                               columnTable,
                               qualifier)
                           || HasNullIntroducingJoinForColumnSource(
                               qualifiedJoin.SecondTableReference,
                               aliasMap,
                               columnTable,
                               qualifier);

                case JoinTableReference join:
                    return HasNullIntroducingJoinForColumnSource(
                               join.FirstTableReference,
                               aliasMap,
                               columnTable,
                               qualifier)
                           || HasNullIntroducingJoinForColumnSource(
                               join.SecondTableReference,
                               aliasMap,
                               columnTable,
                               qualifier);

                case JoinParenthesisTableReference joinParen when joinParen.Join is not null:
                    return HasNullIntroducingJoinForColumnSource(
                        joinParen.Join,
                        aliasMap,
                        columnTable,
                        qualifier);

                default:
                    return false;
            }
        }

        private static bool IsNullIntroducingForColumnSource(
            QualifiedJoin join,
            AliasMap aliasMap,
            ResolvedTable columnTable,
            string? qualifier)
        {
            return join.QualifiedJoinType switch
            {
                QualifiedJoinType.LeftOuter => SideContainsColumnSource(
                    join.SecondTableReference,
                    aliasMap,
                    columnTable,
                    qualifier),
                QualifiedJoinType.RightOuter => SideContainsColumnSource(
                    join.FirstTableReference,
                    aliasMap,
                    columnTable,
                    qualifier),
                QualifiedJoinType.FullOuter => SideContainsColumnSource(
                                                   join.FirstTableReference,
                                                   aliasMap,
                                                   columnTable,
                                                   qualifier)
                                               || SideContainsColumnSource(
                                                   join.SecondTableReference,
                                                   aliasMap,
                                                   columnTable,
                                                   qualifier),
                _ => false
            };
        }

        private static bool SideContainsColumnSource(
            TableReference side,
            AliasMap aliasMap,
            ResolvedTable columnTable,
            string? qualifier)
        {
            var aliases = CollectSideAliases(side);
            if (aliases.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                return aliases.Contains(qualifier);
            }

            foreach (var alias in aliases)
            {
                if (aliasMap.TryResolve(alias, out var resolved) &&
                    resolved is not null &&
                    TablesAreEqual(resolved, columnTable))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> CollectSideAliases(TableReference side)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectSideAliasesCore(side, aliases);
            return aliases;
        }

        private static void CollectSideAliasesCore(TableReference tableRef, HashSet<string> aliases)
        {
            switch (tableRef)
            {
                case JoinTableReference join:
                    CollectSideAliasesCore(join.FirstTableReference, aliases);
                    CollectSideAliasesCore(join.SecondTableReference, aliases);
                    return;
                case JoinParenthesisTableReference joinParen when joinParen.Join is not null:
                    CollectSideAliasesCore(joinParen.Join, aliases);
                    return;
            }

            var alias = TableReferenceHelpers.GetAliasOrTableName(tableRef);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                aliases.Add(alias);
            }
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b) =>
            string.Equals(a.DatabaseName, b.DatabaseName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);
    }
}
