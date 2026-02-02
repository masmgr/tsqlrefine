using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

public sealed class InsertColumnCountMismatchRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/insert-column-count-mismatch",
        Description: "Detects column count mismatches between the target column list and the source in INSERT statements.",
        Category: "Correctness",
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

        var visitor = new InsertColumnCountMismatchVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class InsertColumnCountMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            if (node.InsertSpecification?.Columns == null || node.InsertSpecification.Columns.Count == 0)
            {
                // No explicit column list - can't verify without schema
                base.ExplicitVisit(node);
                return;
            }

            var targetColumnCount = node.InsertSpecification.Columns.Count;

            switch (node.InsertSpecification.InsertSource)
            {
                case SelectInsertSource selectSource:
                    var sourceColumnCount = CountSelectElements(selectSource.Select);
                    if (sourceColumnCount.HasValue && sourceColumnCount.Value != targetColumnCount)
                    {
                        AddDiagnostic(
                            fragment: node,
                            message: $"Column count mismatch in INSERT statement. Target has {targetColumnCount} column(s), but SELECT provides {sourceColumnCount.Value} column(s).",
                            code: "semantic/insert-column-count-mismatch",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                    break;

                case ValuesInsertSource valuesSource:
                    foreach (var rowValue in valuesSource.RowValues)
                    {
                        var valueCount = rowValue.ColumnValues?.Count ?? 0;
                        if (valueCount != targetColumnCount)
                        {
                            AddDiagnostic(
                                fragment: node,
                                message: $"Column count mismatch in INSERT statement. Target has {targetColumnCount} column(s), but VALUES provides {valueCount} value(s).",
                                code: "semantic/insert-column-count-mismatch",
                                category: "Correctness",
                                fixable: false
                            );
                            break; // Report once per INSERT statement
                        }
                    }
                    break;
            }

            base.ExplicitVisit(node);
        }

        private static int? CountSelectElements(QueryExpression? queryExpression)
        {
            if (queryExpression == null)
            {
                return null;
            }

            // Handle QuerySpecification (the main SELECT)
            if (queryExpression is QuerySpecification querySpec)
            {
                if (querySpec.SelectElements == null || querySpec.SelectElements.Count == 0)
                {
                    return 0;
                }

                // If SELECT contains *, we can't determine column count without schema
                if (querySpec.SelectElements.Any(e => e is SelectStarExpression))
                {
                    return null;
                }

                return querySpec.SelectElements.Count;
            }

            // For other query expressions (UNION, etc.), we can't easily count
            // without more complex logic, so return null
            return null;
        }
    }
}
