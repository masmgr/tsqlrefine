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
        private HashSet<string>? _currentSelectAliases;
        private bool _selectAliasesAllowed;
        private readonly List<AliasMap> _outerAliasMaps = [];
        private readonly DmlTargetColumnScopeManager _dmlTargetScopes = new(schema);
        private Dictionary<string, IReadOnlyList<ResolvedTable>>? _unqualifiedColumnMatchesCache;

        public override void ExplicitVisit(QuerySpecification node)
        {
            var previousSelectAliases = _currentSelectAliases;
            var previousSelectAliasesAllowed = _selectAliasesAllowed;
            _currentSelectAliases = CollectSelectAliases(node.SelectElements);
            _selectAliasesAllowed = false;

            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                var previousUnqualifiedColumnMatchesCache = _unqualifiedColumnMatchesCache;

                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);
                _unqualifiedColumnMatchesCache = null;

                if (previousMap is not null)
                {
                    _outerAliasMaps.Add(previousMap);
                }

                try
                {
                    // Visit all clauses with the alias map in scope
                    QuerySpecificationChildVisitor.VisitChildren(this, node);
                }
                finally
                {
                    if (previousMap is not null)
                    {
                        _outerAliasMaps.RemoveAt(_outerAliasMaps.Count - 1);
                    }

                    _currentAliasMap = previousMap;
                    _currentSelectAliases = previousSelectAliases;
                    _selectAliasesAllowed = previousSelectAliasesAllowed;
                    _unqualifiedColumnMatchesCache = previousUnqualifiedColumnMatchesCache;
                }

                return; // We manually visited children
            }

            try
            {
                // Keep the outer alias map for correlated references, but do not let
                // output aliases from the outer query leak into this query.
                QuerySpecificationChildVisitor.VisitChildren(this, node);
            }
            finally
            {
                _currentSelectAliases = previousSelectAliases;
                _selectAliasesAllowed = previousSelectAliasesAllowed;
            }
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (!_dmlTargetScopes.TryPush(node.UpdateSpecification))
            {
                base.ExplicitVisit(node);
                return;
            }

            try
            {
                node.AcceptChildren(this);
            }
            finally
            {
                _dmlTargetScopes.Pop();
            }
        }

        public override void ExplicitVisit(OrderByClause node)
        {
            var previousSelectAliasesAllowed = _selectAliasesAllowed;
            _selectAliasesAllowed = true;

            try
            {
                base.ExplicitVisit(node);
            }
            finally
            {
                _selectAliasesAllowed = previousSelectAliasesAllowed;
            }
        }

        public override void ExplicitVisit(CommonTableExpression node)
        {
            // Don't validate columns inside CTE definitions
            // (they may reference their own CTE columns)
            base.ExplicitVisit(node);
        }

        private static HashSet<string>? CollectSelectAliases(IList<SelectElement>? elements)
        {
            if (elements is null)
            {
                return null;
            }

            HashSet<string>? aliases = null;
            foreach (var element in elements)
            {
                if (element is SelectScalarExpression { ColumnName.Value: { } aliasName })
                {
                    aliases ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    aliases.Add(aliasName);
                }
            }

            return aliases;
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (_currentAliasMap is not null && node.ColumnType != ColumnType.Wildcard)
            {
                var identifiers = node.MultiPartIdentifier?.Identifiers;
                if (identifiers is { Count: 1 })
                {
                    ValidateUnqualifiedColumn(node, identifiers[0].Value);
                }
                else if (identifiers is { Count: > 1 })
                {
                    ValidateQualifiedColumn(node, identifiers);
                }
            }

            base.ExplicitVisit(node);
        }

        private void ValidateQualifiedColumn(ColumnReferenceExpression node, IList<Identifier> identifiers)
        {
            var columnName = identifiers[identifiers.Count - 1].Value;

            if (!TryResolveQualifiedTable(identifiers, out var resolvedTable))
            {
                // Qualifier not in current scope — skip (may be outer scope)
                return;
            }

            if (resolvedTable is null)
            {
                // Unresolvable (CTE, derived table, temp table) — skip
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

        private void ValidateUnqualifiedColumn(ColumnReferenceExpression node, string columnName)
        {
            // SELECT aliases are visible in ORDER BY and take precedence over
            // source-column resolution there.
            if (_selectAliasesAllowed && _currentSelectAliases?.Contains(columnName) == true)
            {
                return;
            }

            var matches = FindTablesContainingColumn(columnName);

            if (matches.Count > 1)
            {
                ReportAmbiguousColumn(node, columnName, matches);
                return;
            }

            if (matches.Count == 1 || _currentAliasMap!.AllTables.Count == 0)
            {
                return;
            }

            // The column may belong to an unverifiable source (CTE, derived table,
            // temp table, ...) — no conclusion can be drawn.
            if (_currentAliasMap.HasUnresolvableEntries)
            {
                return;
            }

            var (outerMatches, outerIndeterminate) = FindColumnInOuterScopes(columnName);
            if (outerMatches.Count == 1 || outerIndeterminate)
            {
                return;
            }

            if (outerMatches.Count > 1)
            {
                ReportAmbiguousColumn(node, columnName, outerMatches);
                return;
            }

            if (_dmlTargetScopes.CanResolve(columnName))
            {
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: $"Column '{columnName}' not found in any table in the current scope.",
                code: "unresolved-column-reference",
                category: "Schema",
                fixable: false
            );
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
            return schema.ResolveColumn(table, columnName) is not null;
        }

        private IReadOnlyList<ResolvedTable> FindTablesContainingColumn(string columnName)
        {
            if (_unqualifiedColumnMatchesCache?.TryGetValue(columnName, out var cached) == true)
            {
                return cached;
            }

            if (_currentAliasMap is null || _currentAliasMap.AllTables.Count == 0)
            {
                cached = Array.Empty<ResolvedTable>();
                (_unqualifiedColumnMatchesCache ??= new(StringComparer.OrdinalIgnoreCase))[columnName] = cached;
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
            (_unqualifiedColumnMatchesCache ??= new(StringComparer.OrdinalIgnoreCase))[columnName] = cached;
            return cached;
        }

        private (IReadOnlyList<ResolvedTable> Matches, bool Indeterminate) FindColumnInOuterScopes(string columnName)
        {
            for (var i = _outerAliasMaps.Count - 1; i >= 0; i--)
            {
                var matches = new List<ResolvedTable>();
                foreach (var table in _outerAliasMaps[i].AllTables)
                {
                    if (schema.ResolveColumn(table, columnName) is not null)
                    {
                        matches.Add(table);
                    }
                }

                if (matches.Count > 0)
                {
                    return (matches, false);
                }

                if (_outerAliasMaps[i].HasUnresolvableEntries)
                {
                    // The column may come from an unverifiable source in this scope.
                    return (Array.Empty<ResolvedTable>(), true);
                }
            }

            return (Array.Empty<ResolvedTable>(), false);
        }

        private void ReportAmbiguousColumn(
            TSqlFragment fragment,
            string columnName,
            IReadOnlyList<ResolvedTable> matches)
        {
            var tableNames = string.Join(", ",
                matches.Select(t => $"{t.SchemaName}.{t.TableName}"));
            AddDiagnostic(
                fragment: fragment,
                message: $"Ambiguous column reference '{columnName}' (found in: {tableNames}).",
                code: "unresolved-column-reference",
                category: "Schema",
                fixable: false
            );
        }

    }
}
