using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in CREATE VIEW definitions.
/// Duplicate columns in a VIEW always cause a runtime error.
/// </summary>
public sealed class DuplicateViewColumnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-view-column",
        Description: "Detects duplicate column names in CREATE VIEW definitions; duplicate columns always cause a runtime error.",
        Category: "Schema",
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

        var visitor = new DuplicateViewColumnVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateViewColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateViewStatement node)
        {
            // If the VIEW has an explicit column list, check that for duplicates
            if (node.Columns != null && node.Columns.Count > 0)
            {
                CheckExplicitColumnList(node.Columns);
            }
            else
            {
                // Otherwise check the SELECT elements for duplicate output names
                var querySpec = node.SelectStatement?.QueryExpression as QuerySpecification;
                if (querySpec?.SelectElements != null)
                {
                    CheckSelectElements(querySpec.SelectElements);
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckExplicitColumnList(IList<Identifier> columns)
        {
            var seen = new Dictionary<string, Identifier>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                var name = column.Value;
                if (name == null)
                {
                    continue;
                }

                if (seen.ContainsKey(name))
                {
                    AddDiagnostic(
                        fragment: column,
                        message: $"Column '{name}' appears more than once in the VIEW column list.",
                        code: "duplicate-view-column",
                        category: "Schema",
                        fixable: false
                    );
                }
                else
                {
                    seen[name] = column;
                }
            }
        }

        private void CheckSelectElements(IList<SelectElement> selectElements)
        {
            foreach (var (element, name) in SelectColumnHelpers.FindDuplicateColumns(selectElements))
            {
                AddDiagnostic(
                    fragment: (TSqlFragment)element,
                    message: $"Column '{name}' appears more than once in the VIEW SELECT list.",
                    code: "duplicate-view-column",
                    category: "Schema",
                    fixable: false
                );
            }
        }
    }
}
