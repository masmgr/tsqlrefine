using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects references to columns that do not exist in the schema snapshot.
/// </summary>
public sealed class UnresolvedColumnReferenceRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "unresolved-column-reference",
        Description: "Detects references to columns that do not exist in the schema snapshot.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnresolvedColumnReferenceVisitor(context.Schema!);

    private sealed class UnresolvedColumnReferenceVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;
        private Dictionary<(ResolvedTable Table, string ColumnName), bool> _columnExistsCache =
            new(ResolvedTableComparers.TableColumnKeyComparer.Instance);
        private Dictionary<string, IReadOnlyList<ResolvedTable>> _unqualifiedColumnMatchesCache =
            new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                var previousColumnExistsCache = _columnExistsCache;
                var previousUnqualifiedColumnMatchesCache = _unqualifiedColumnMatchesCache;

                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);
                _columnExistsCache = new Dictionary<(ResolvedTable Table, string ColumnName), bool>(ResolvedTableComparers.TableColumnKeyComparer.Instance);
                _unqualifiedColumnMatchesCache = new Dictionary<string, IReadOnlyList<ResolvedTable>>(StringComparer.OrdinalIgnoreCase);

                // Visit SELECT list, WHERE, etc. with the alias map in scope
                VisitSelectElements(node.SelectElements);
                node.WhereClause?.Accept(this);
                node.HavingClause?.Accept(this);
                node.OrderByClause?.Accept(this);
                node.GroupByClause?.Accept(this);

                // Visit FROM clause for join conditions
                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                _columnExistsCache = previousColumnExistsCache;
                _unqualifiedColumnMatchesCache = previousUnqualifiedColumnMatchesCache;
                return; // We manually visited children
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommonTableExpression node)
        {
            // Don't validate columns inside CTE definitions
            // (they may reference their own CTE columns)
            base.ExplicitVisit(node);
        }

        private void VisitSelectElements(IList<SelectElement>? elements)
        {
            if (elements is null)
            {
                return;
            }

            foreach (var element in elements)
            {
                element.Accept(this);
            }
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (_currentAliasMap is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (node.ColumnType == ColumnType.Wildcard)
            {
                base.ExplicitVisit(node);
                return;
            }

            var identifiers = node.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                base.ExplicitVisit(node);
                return;
            }

            if (identifiers.Count >= 2)
            {
                var columnName = identifiers[identifiers.Count - 1].Value;

                if (!TryResolveQualifiedTable(identifiers, out var resolvedTable))
                {
                    // Qualifier not in current scope — skip (may be outer scope)
                    base.ExplicitVisit(node);
                    return;
                }

                if (resolvedTable is null)
                {
                    // Unresolvable (CTE, derived table, temp table) — skip
                    base.ExplicitVisit(node);
                    return;
                }

                if (!ColumnExists(resolvedTable, columnName))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Column '{columnName}' not found in '{resolvedTable.SchemaName}.{resolvedTable.TableName}'.",
                        code: "unresolved-column-reference",
                        category: "Schema",
                        fixable: false
                    );
                }
            }
            else
            {
                // Unqualified column — search all tables
                var columnName = identifiers[0].Value;
                var matches = FindTablesContainingColumn(columnName);

                if (matches.Count == 0 && _currentAliasMap.AllTables.Count > 0)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Column '{columnName}' not found in any table in the current scope.",
                        code: "unresolved-column-reference",
                        category: "Schema",
                        fixable: false
                    );
                }
                else if (matches.Count > 1)
                {
                    var tableNames = string.Join(", ",
                        matches.Select(t => $"{t.SchemaName}.{t.TableName}"));
                    AddDiagnostic(
                        fragment: node,
                        message: $"Ambiguous column reference '{columnName}' (found in: {tableNames}).",
                        code: "unresolved-column-reference",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private bool TryResolveQualifiedTable(IList<Identifier> identifiers, out ResolvedTable? resolvedTable)
        {
            if (_currentAliasMap is null)
            {
                resolvedTable = null;
                return false;
            }

            return QualifierLookupKeyBuilder.TryResolve(_currentAliasMap, identifiers, out resolvedTable);
        }

        private bool ColumnExists(ResolvedTable table, string columnName)
        {
            if (_columnExistsCache.TryGetValue((table, columnName), out var exists))
            {
                return exists;
            }

            exists = schema.ResolveColumn(table, columnName) is not null;
            _columnExistsCache[(table, columnName)] = exists;
            return exists;
        }

        private IReadOnlyList<ResolvedTable> FindTablesContainingColumn(string columnName)
        {
            if (_unqualifiedColumnMatchesCache.TryGetValue(columnName, out var cached))
            {
                return cached;
            }

            if (_currentAliasMap is null || _currentAliasMap.AllTables.Count == 0)
            {
                cached = Array.Empty<ResolvedTable>();
                _unqualifiedColumnMatchesCache[columnName] = cached;
                return cached;
            }

            var matches = new List<ResolvedTable>();
            foreach (var table in _currentAliasMap.AllTables)
            {
                if (ColumnExists(table, columnName))
                {
                    matches.Add(table);
                }
            }

            cached = matches.Count == 0 ? Array.Empty<ResolvedTable>() : matches;
            _unqualifiedColumnMatchesCache[columnName] = cached;
            return cached;
        }

    }
}
