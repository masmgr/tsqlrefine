using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects column count mismatches between the target column list and the source in INSERT statements.
/// </summary>
public sealed class InsertColumnCountMismatchRule : IRule
{
    private const string RuleId = "semantic/insert-column-count-mismatch";
    private const string Category = "Correctness";

    public RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects column count mismatches between the target column list and the source in INSERT statements.",
        Category: Category,
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
            var insertSpec = node.InsertSpecification;
            if (insertSpec?.Columns is not { Count: > 0 })
            {
                // No explicit column list - can't verify without schema
                base.ExplicitVisit(node);
                return;
            }

            var targetColumnCount = insertSpec.Columns.Count;

            switch (insertSpec.InsertSource)
            {
                case SelectInsertSource selectSource:
                    CheckSelectInsertSource(node, selectSource, targetColumnCount);
                    break;

                case ValuesInsertSource valuesSource:
                    CheckValuesInsertSource(node, valuesSource, targetColumnCount);
                    break;
            }

            base.ExplicitVisit(node);
        }

        private void CheckSelectInsertSource(InsertStatement node, SelectInsertSource selectSource, int targetColumnCount)
        {
            var sourceColumnCount = CountSelectElements(selectSource.Select);
            if (sourceColumnCount is not null && sourceColumnCount.Value != targetColumnCount)
            {
                AddDiagnostic(
                    fragment: node,
                    message: $"Column count mismatch in INSERT statement. Target has {targetColumnCount} column(s), but SELECT provides {sourceColumnCount.Value} column(s).",
                    code: RuleId,
                    category: Category,
                    fixable: false
                );
            }
        }

        private void CheckValuesInsertSource(InsertStatement node, ValuesInsertSource valuesSource, int targetColumnCount)
        {
            foreach (var rowValue in valuesSource.RowValues)
            {
                var valueCount = rowValue.ColumnValues?.Count ?? 0;
                if (valueCount != targetColumnCount)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Column count mismatch in INSERT statement. Target has {targetColumnCount} column(s), but VALUES provides {valueCount} value(s).",
                        code: RuleId,
                        category: Category,
                        fixable: false
                    );
                    return; // Report once per INSERT statement
                }
            }
        }

        private static int? CountSelectElements(QueryExpression? queryExpression)
        {
            // Only QuerySpecification (main SELECT) can be reliably counted
            // UNION/INTERSECT/EXCEPT require more complex analysis
            if (queryExpression is not QuerySpecification querySpec)
            {
                return null;
            }

            if (querySpec.SelectElements is not { Count: > 0 })
            {
                return 0;
            }

            // SELECT * or table.* can't be counted without schema information
            if (querySpec.SelectElements.Any(e => e is SelectStarExpression))
            {
                return null;
            }

            return querySpec.SelectElements.Count;
        }
    }
}
