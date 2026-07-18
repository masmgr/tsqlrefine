using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

/// <summary>
/// Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.
/// </summary>
public sealed class DmlWithoutWhereRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "dml-without-where",
        Description: "Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.",
        Category: "Safety",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DmlWithoutWhereVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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

            if (HasInnerJoin(fromClause))
            {
                return;
            }

            AddDiagnostic(
                range: ScriptDomHelpers.GetFirstTokenRange(node),
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
