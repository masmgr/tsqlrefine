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

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);

                // Visit SELECT list, WHERE, etc. with the alias map in scope
                VisitSelectElements(node.SelectElements);
                node.WhereClause?.Accept(this);
                node.HavingClause?.Accept(this);
                node.OrderByClause?.Accept(this);
                node.GroupByClause?.Accept(this);

                // Visit FROM clause for join conditions
                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
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

                var resolvedColumn = schema.ResolveColumn(resolvedTable, columnName);
                if (resolvedColumn is null)
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
                var matches = new List<ResolvedTable>();

                foreach (var table in _currentAliasMap.AllTables)
                {
                    if (schema.ResolveColumn(table, columnName) is not null)
                    {
                        matches.Add(table);
                    }
                }

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

            foreach (var key in QualifierLookupKeyBuilder.Build(identifiers))
            {
                if (_currentAliasMap.TryResolve(key, out resolvedTable))
                {
                    return true;
                }
            }

            resolvedTable = null;
            return false;
        }

    }
}
