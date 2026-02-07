using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class InsertSelectColumnNameMismatchRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "insert-select-column-name-mismatch",
        Description: "Warns when INSERT target column names do not match SELECT output column names in INSERT ... SELECT statements.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new InsertSelectColumnNameMismatchVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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

            if (selectSource.Select is not QuerySpecification querySpec || querySpec.SelectElements == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (querySpec.SelectElements.Count != insertSpec.Columns.Count)
            {
                // Column count mismatch is handled by a separate rule.
                base.ExplicitVisit(node);
                return;
            }

            if (!TryGetInsertTargetNames(insertSpec.Columns, out var targetNames) ||
                !TryGetSelectOutputNames(querySpec.SelectElements, out var selectNames))
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
