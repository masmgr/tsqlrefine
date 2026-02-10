using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

/// <summary>
/// Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.
/// </summary>
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
            CheckDmlWithoutWhere(
                node.UpdateSpecification?.Target,
                node.UpdateSpecification?.FromClause,
                node.UpdateSpecification?.WhereClause,
                node,
                "UPDATE",
                "modify");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            CheckDmlWithoutWhere(
                node.DeleteSpecification?.Target,
                node.DeleteSpecification?.FromClause,
                node.DeleteSpecification?.WhereClause,
                node,
                "DELETE",
                "delete");
            base.ExplicitVisit(node);
        }

        private void CheckDmlWithoutWhere(
            TableReference? target,
            FromClause? fromClause,
            WhereClause? whereClause,
            TSqlFragment node,
            string statementType,
            string actionVerb)
        {
            if (whereClause is not null)
            {
                return;
            }

            if (IsTemporaryTableOrTableVariable(target))
            {
                return;
            }

            if (IsTargetUsingAlias(target, fromClause))
            {
                return;
            }

            if (HasInnerJoin(fromClause))
            {
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: $"{statementType} statement without WHERE clause can {actionVerb} all rows. Add a WHERE clause to limit the scope.",
                code: "dml-without-where",
                category: "Safety",
                fixable: false
            );
        }

        private static bool IsTemporaryTableOrTableVariable(TableReference? target)
        {
            // Table variables (@tablevar) are represented as VariableTableReference
            if (target is VariableTableReference)
            {
                return true;
            }

            // Temporary tables (#temp, ##global) are NamedTableReference with # prefix
            if (target is NamedTableReference namedTable &&
                ScriptDomHelpers.IsTemporaryTableName(namedTable.SchemaObject?.BaseIdentifier?.Value))
            {
                return true;
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

            return TableReferenceHelpers.CollectJoinsOfType(
                fromClause.TableReferences,
                QualifiedJoinType.Inner).Any();
        }
    }
}
