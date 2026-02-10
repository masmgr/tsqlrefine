using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in DECLARE @table TABLE variable definitions.
/// Duplicate columns in a table variable always cause a runtime error.
/// </summary>
public sealed class DuplicateTableVariableColumnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-table-variable-column",
        Description: "Detects duplicate column names in DECLARE @table TABLE variable definitions; duplicate columns always cause a runtime error.",
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

        var visitor = new DuplicateTableVariableColumnVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateTableVariableColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DeclareTableVariableStatement node)
        {
            if (node.Body?.Definition?.ColumnDefinitions != null)
            {
                var seen = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);

                foreach (var column in node.Body.Definition.ColumnDefinitions)
                {
                    var name = column.ColumnIdentifier?.Value;
                    if (name == null)
                    {
                        continue;
                    }

                    if (seen.ContainsKey(name))
                    {
                        AddDiagnostic(
                            fragment: column,
                            message: $"Column '{name}' is defined more than once in the same DECLARE TABLE variable.",
                            code: "duplicate-table-variable-column",
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

            base.ExplicitVisit(node);
        }
    }
}
