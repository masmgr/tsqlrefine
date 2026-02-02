using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Safety;

public sealed class DmlWithoutWhereRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "dml-without-where",
        Description: "Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.",
        Category: "Safety",
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

        var visitor = new DmlWithoutWhereVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DmlWithoutWhereVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
            {
                var target = node.UpdateSpecification?.Target;
                var fromClause = node.UpdateSpecification?.FromClause;

                // Skip temporary tables and table variables
                if (IsTemporaryTableOrTableVariable(target))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip when target uses an alias (implies FROM clause with more complex logic)
                if (IsTargetUsingAlias(target, fromClause))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip when FROM clause contains INNER JOIN (inherent filtering)
                if (HasInnerJoin(fromClause))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                AddDiagnostic(
                    fragment: node,
                    message: "UPDATE statement without WHERE clause can modify all rows. Add a WHERE clause to limit the scope.",
                    code: "dml-without-where",
                    category: "Safety",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
            {
                var target = node.DeleteSpecification?.Target;
                var fromClause = node.DeleteSpecification?.FromClause;

                // Skip temporary tables and table variables
                if (IsTemporaryTableOrTableVariable(target))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip when target uses an alias (implies FROM clause with more complex logic)
                if (IsTargetUsingAlias(target, fromClause))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                // Skip when FROM clause contains INNER JOIN (inherent filtering)
                if (HasInnerJoin(fromClause))
                {
                    base.ExplicitVisit(node);
                    return;
                }

                AddDiagnostic(
                    fragment: node,
                    message: "DELETE statement without WHERE clause can delete all rows. Add a WHERE clause to limit the scope.",
                    code: "dml-without-where",
                    category: "Safety",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool IsTemporaryTableOrTableVariable(TableReference? target)
        {
            // Table variables (@tablevar) are represented as VariableTableReference
            if (target is VariableTableReference)
            {
                return true;
            }

            // Temporary tables (#temp, ##global) are NamedTableReference with # prefix
            if (target is NamedTableReference namedTable)
            {
                var tableName = namedTable.SchemaObject?.BaseIdentifier?.Value;
                if (tableName != null && tableName.StartsWith('#'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTargetUsingAlias(TableReference? target, FromClause? fromClause)
        {
            if (target is not NamedTableReference targetTable || fromClause?.TableReferences is null)
            {
                return false;
            }

            var targetName = targetTable.SchemaObject?.BaseIdentifier?.Value;
            if (targetName is null)
            {
                return false;
            }

            // Collect only explicit aliases (not table names) from FROM clause
            var explicitAliases = CollectExplicitAliases(fromClause.TableReferences);

            // If target name matches an explicit alias defined in FROM clause, it's using an alias
            return explicitAliases.Contains(targetName) && targetTable.Alias is null;
        }

        private static HashSet<string> CollectExplicitAliases(IList<TableReference> tableRefs)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExplicitAliasesCore(tableRefs, aliases);
            return aliases;
        }

        private static void CollectExplicitAliasesCore(IList<TableReference> tableRefs, HashSet<string> aliases)
        {
            foreach (var tableRef in tableRefs)
            {
                if (tableRef is JoinTableReference join)
                {
                    CollectExplicitAliasesCore([join.FirstTableReference], aliases);
                    CollectExplicitAliasesCore([join.SecondTableReference], aliases);
                }
                else if (tableRef is NamedTableReference namedTable && namedTable.Alias?.Value is not null)
                {
                    // Only add explicit aliases, not table names
                    aliases.Add(namedTable.Alias.Value);
                }
                else if (tableRef is VariableTableReference varTable && varTable.Alias?.Value is not null)
                {
                    aliases.Add(varTable.Alias.Value);
                }
                else if (tableRef is QueryDerivedTable derivedTable && derivedTable.Alias?.Value is not null)
                {
                    aliases.Add(derivedTable.Alias.Value);
                }
            }
        }

        private static bool HasInnerJoin(FromClause? fromClause)
        {
            if (fromClause?.TableReferences is null)
            {
                return false;
            }

            return ContainsInnerJoin(fromClause.TableReferences);
        }

        private static bool ContainsInnerJoin(IList<TableReference> tableReferences)
        {
            foreach (var tableRef in tableReferences)
            {
                if (tableRef is QualifiedJoin qualifiedJoin)
                {
                    if (qualifiedJoin.QualifiedJoinType == QualifiedJoinType.Inner)
                    {
                        return true;
                    }

                    if (ContainsInnerJoin([qualifiedJoin.FirstTableReference]) ||
                        ContainsInnerJoin([qualifiedJoin.SecondTableReference]))
                    {
                        return true;
                    }
                }
                else if (tableRef is JoinTableReference join)
                {
                    if (ContainsInnerJoin([join.FirstTableReference]) ||
                        ContainsInnerJoin([join.SecondTableReference]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
