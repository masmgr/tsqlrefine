using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects references to tables or views that do not exist in the schema snapshot.
/// </summary>
public sealed class UnresolvedTableReferenceRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "unresolved-table-reference",
        Description: "Detects references to tables or views that do not exist in the schema snapshot.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnresolvedTableReferenceVisitor(context.Schema!);

    private sealed class UnresolvedTableReferenceVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private static readonly FrozenSet<string> ExcludedSchemas =
            FrozenSet.ToFrozenSet(["sys", "INFORMATION_SCHEMA"], StringComparer.OrdinalIgnoreCase);
        private readonly Stack<HashSet<string>> _cteScopes = new();
        private readonly Stack<HashSet<string>> _dmlAliasScopes = new();

        public override void ExplicitVisit(SelectStatement node)
        {
            VisitWithCteScope(
                node.WithCtesAndXmlNamespaces,
                () => base.ExplicitVisit(node));
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            VisitWithCteScope(
                node.WithCtesAndXmlNamespaces,
                () => base.ExplicitVisit(node));
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            VisitWithCteScope(
                node.WithCtesAndXmlNamespaces,
                () => VisitWithDmlAliasScope(
                    node.UpdateSpecification?.FromClause?.TableReferences,
                    () => base.ExplicitVisit(node)));
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            VisitWithCteScope(
                node.WithCtesAndXmlNamespaces,
                () => VisitWithDmlAliasScope(
                    node.DeleteSpecification?.FromClause?.TableReferences,
                    () => base.ExplicitVisit(node)));
        }

        public override void ExplicitVisit(MergeStatement node)
        {
            VisitWithCteScope(
                node.WithCtesAndXmlNamespaces,
                () => base.ExplicitVisit(node));
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            var schemaObject = node.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip temp tables and table variables
            if (tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip system schemas
            var schemaName = schemaObject.SchemaIdentifier?.Value;
            if (schemaName is not null && ExcludedSchemas.Contains(schemaName))
            {
                base.ExplicitVisit(node);
                return;
            }

            var dbName = schemaObject.DatabaseIdentifier?.Value;
            if (ShouldSkipLookup(node, dbName, schemaName, tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            var resolved = schema.ResolveTable(dbName, schemaName, tableName);

            if (resolved is null)
            {
                var fullName = FormatTableName(dbName, schemaName, tableName);
                AddDiagnostic(
                    fragment: node,
                    message: $"Table or view '{fullName}' not found in schema snapshot.",
                    code: "unresolved-table-reference",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private void VisitWithDmlAliasScope(IList<TableReference>? tableRefs, Action visitAction)
        {
            var aliases = CollectExplicitAliases(tableRefs);
            if (aliases.Count == 0)
            {
                visitAction();
                return;
            }

            _dmlAliasScopes.Push(aliases);
            try
            {
                visitAction();
            }
            finally
            {
                _dmlAliasScopes.Pop();
            }
        }

        private bool ShouldSkipLookup(NamedTableReference node, string? dbName, string? schemaName, string tableName)
        {
            // CTE references are represented as NamedTableReference but are not base objects.
            if (dbName is null && schemaName is null && IsInCteScope(tableName))
            {
                return true;
            }

            // UPDATE/DELETE alias targets (e.g., UPDATE u FROM dbo.Users AS u) are not table names.
            if (dbName is null && schemaName is null && node.Alias is null && IsInDmlAliasScope(tableName))
            {
                return true;
            }

            return false;
        }

        private bool IsInCteScope(string tableName) =>
            _cteScopes.Any(scope => scope.Contains(tableName));

        private bool IsInDmlAliasScope(string tableName) =>
            _dmlAliasScopes.Any(scope => scope.Contains(tableName));

        private void VisitWithCteScope(WithCtesAndXmlNamespaces? withCtes, Action visitAction)
        {
            var cteNames = CollectCteNames(withCtes);
            if (cteNames.Count == 0)
            {
                visitAction();
                return;
            }

            _cteScopes.Push(cteNames);
            try
            {
                visitAction();
            }
            finally
            {
                _cteScopes.Pop();
            }
        }

        private static HashSet<string> CollectCteNames(WithCtesAndXmlNamespaces? withCtes)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cte in withCtes?.CommonTableExpressions ?? [])
            {
                var cteName = cte.ExpressionName?.Value;
                if (!string.IsNullOrWhiteSpace(cteName))
                {
                    names.Add(cteName);
                }
            }

            return names;
        }

        private static HashSet<string> CollectExplicitAliases(IList<TableReference>? tableRefs)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tableRefs is null)
            {
                return aliases;
            }

            foreach (var tableRef in tableRefs)
            {
                CollectExplicitAliasesCore(tableRef, aliases);
            }

            return aliases;
        }

        private static void CollectExplicitAliasesCore(TableReference tableRef, HashSet<string> aliases)
        {
            switch (tableRef)
            {
                case JoinTableReference join:
                    CollectExplicitAliasesCore(join.FirstTableReference, aliases);
                    CollectExplicitAliasesCore(join.SecondTableReference, aliases);
                    return;
                case JoinParenthesisTableReference joinParenthesis when joinParenthesis.Join is not null:
                    CollectExplicitAliasesCore(joinParenthesis.Join, aliases);
                    return;
            }

            var alias = GetExplicitAlias(tableRef);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                aliases.Add(alias);
            }
        }

        private static string? GetExplicitAlias(TableReference tableRef) =>
            tableRef switch
            {
                NamedTableReference t => t.Alias?.Value,
                QueryDerivedTable t => t.Alias?.Value,
                SchemaObjectFunctionTableReference t => t.Alias?.Value,
                VariableTableReference t => t.Alias?.Value,
                BuiltInFunctionTableReference t => t.Alias?.Value,
                OpenJsonTableReference t => t.Alias?.Value,
                OpenXmlTableReference t => t.Alias?.Value,
                OpenRowsetTableReference t => t.Alias?.Value,
                OpenQueryTableReference t => t.Alias?.Value,
                FullTextTableReference t => t.Alias?.Value,
                PivotedTableReference t => t.Alias?.Value,
                UnpivotedTableReference t => t.Alias?.Value,
                InlineDerivedTable t => t.Alias?.Value,
                ChangeTableChangesTableReference t => t.Alias?.Value,
                ChangeTableVersionTableReference t => t.Alias?.Value,
                DataModificationTableReference t => t.Alias?.Value,
                SemanticTableReference t => t.Alias?.Value,
                GlobalFunctionTableReference t => t.Alias?.Value,
                AdHocTableReference t => t.Alias?.Value,
                _ => null
            };

        private static string FormatTableName(string? db, string? schema, string name)
        {
            if (db is not null)
            {
                return $"{db}.{schema ?? "dbo"}.{name}";
            }

            return schema is not null ? $"{schema}.{name}" : name;
        }
    }
}
