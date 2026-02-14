using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Warns when INSERT target column names do not match SELECT output column names in INSERT ... SELECT statements.
/// </summary>
public sealed class InsertSelectColumnNameMismatchRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "insert-select-column-name-mismatch",
        Description: "Warns when INSERT target column names do not match SELECT output column names in INSERT ... SELECT statements.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new InsertSelectColumnNameMismatchVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class InsertSelectColumnNameMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            var insertSpec = node.InsertSpecification;
            if (insertSpec?.Columns == null || insertSpec.Columns.Count == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (insertSpec.InsertSource is not SelectInsertSource selectSource)
            {
                base.ExplicitVisit(node);
                return;
            }

            var selectElements = GetSelectOutputElements(selectSource.Select);
            if (selectElements == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (selectElements.Count != insertSpec.Columns.Count)
            {
                // Column count mismatch is handled by a separate rule.
                base.ExplicitVisit(node);
                return;
            }

            if (!TryGetInsertTargetNames(insertSpec.Columns, out var targetNames) ||
                !TryGetSelectOutputNames(selectElements, out var selectNames))
            {
                base.ExplicitVisit(node);
                return;
            }

            for (var i = 0; i < targetNames.Count; i++)
            {
                if (!string.Equals(targetNames[i], selectNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "INSERT target column names do not match SELECT output column names. This may indicate a swapped column order.",
                        code: "insert-select-column-name-mismatch",
                        category: "Correctness",
                        fixable: false
                    );
                    break;
                }
            }

            base.ExplicitVisit(node);
        }

        private static IList<SelectElement>? GetSelectOutputElements(QueryExpression? queryExpression)
        {
            switch (queryExpression)
            {
                case QuerySpecification querySpec:
                    return querySpec.SelectElements;

                case QueryParenthesisExpression paren:
                    return GetSelectOutputElements(paren.QueryExpression);

                case BinaryQueryExpression binary:
                    // UNION/INTERSECT/EXCEPT output column names are defined by the first query.
                    return GetSelectOutputElements(binary.FirstQueryExpression);
            }

            return null;
        }

        private static bool TryGetInsertTargetNames(IList<ColumnReferenceExpression> columns, out List<string> names)
        {
            names = new List<string>(columns.Count);

            foreach (var column in columns)
            {
                var name = GetLastIdentifier(column.MultiPartIdentifier);
                if (string.IsNullOrWhiteSpace(name))
                {
                    names.Clear();
                    return false;
                }

                names.Add(name);
            }

            return true;
        }

        private static bool TryGetSelectOutputNames(IList<SelectElement> selectElements, out List<string> names)
        {
            names = new List<string>(selectElements.Count);

            foreach (var selectElement in selectElements)
            {
                if (selectElement is not SelectScalarExpression scalar)
                {
                    names.Clear();
                    return false;
                }

                if (scalar.Expression is not ColumnReferenceExpression columnReference)
                {
                    names.Clear();
                    return false;
                }

                var name = GetLastIdentifier(columnReference.MultiPartIdentifier);
                if (string.IsNullOrWhiteSpace(name))
                {
                    names.Clear();
                    return false;
                }

                var alias = scalar.ColumnName?.Value;
                names.Add(string.IsNullOrWhiteSpace(alias) ? name : alias);

            }

            return true;
        }

        private static string? GetLastIdentifier(MultiPartIdentifier? identifier)
        {
            if (identifier == null || identifier.Identifiers.Count == 0)
            {
                return null;
            }

            return identifier.Identifiers[^1].Value;
        }
    }
}
