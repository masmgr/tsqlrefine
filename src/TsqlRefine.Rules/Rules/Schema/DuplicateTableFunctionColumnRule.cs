using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in table-valued function definitions.
/// Applies to both inline TVFs (SELECT list) and multi-statement TVFs (return table column definitions).
/// </summary>
public sealed class DuplicateTableFunctionColumnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-table-function-column",
        Description: "Detects duplicate column names in table-valued function definitions; duplicate columns always cause a runtime error.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DuplicateTableFunctionColumnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateTableFunctionColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateFunctionStatement node)
        {
            // Inline table-valued function: RETURNS TABLE AS RETURN (SELECT ...)
            if (node.ReturnType is SelectFunctionReturnType selectReturn)
            {
                var querySpec = UnwrapQuerySpecification(selectReturn.SelectStatement?.QueryExpression);
                if (querySpec?.SelectElements != null)
                {
                    CheckSelectElements(querySpec.SelectElements);
                }
            }
            // Multi-statement table-valued function: RETURNS @table TABLE (col1 ..., col2 ...)
            else if (node.ReturnType is TableValuedFunctionReturnType tableReturn)
            {
                if (tableReturn.DeclareTableVariableBody?.Definition?.ColumnDefinitions != null)
                {
                    CheckColumnDefinitions(tableReturn.DeclareTableVariableBody.Definition.ColumnDefinitions);
                }
            }

            base.ExplicitVisit(node);
        }

        private static QuerySpecification? UnwrapQuerySpecification(QueryExpression? expression)
        {
            // Unwrap parenthesized queries: RETURN (SELECT ...) produces QueryParenthesisExpression
            while (expression is QueryParenthesisExpression paren)
            {
                expression = paren.QueryExpression;
            }

            return expression as QuerySpecification;
        }

        private void CheckSelectElements(IList<SelectElement> selectElements)
        {
            foreach (var (element, name) in SelectColumnHelpers.FindDuplicateColumns(selectElements))
            {
                AddDiagnostic(
                    fragment: (TSqlFragment)element,
                    message: $"Column '{name}' appears more than once in the table-valued function result.",
                    code: "duplicate-table-function-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }

        private void CheckColumnDefinitions(IList<ColumnDefinition> columns)
        {
            foreach (var duplicate in DuplicateNameAnalysisHelpers.FindDuplicateNames(
                columns,
                column => column.ColumnIdentifier?.Value))
            {
                AddDiagnostic(
                    fragment: duplicate.Item,
                    message: $"Column '{duplicate.Name}' is defined more than once in the table-valued function return table.",
                    code: "duplicate-table-function-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }
    }
}
