using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate column names in CREATE TABLE definitions.
/// </summary>
public sealed class DuplicateColumnDefinitionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-column-definition",
        Description: "Detects duplicate column names in CREATE TABLE definitions; duplicate columns always cause a runtime error.",
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

        var visitor = new DuplicateColumnDefinitionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateColumnDefinitionVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition?.ColumnDefinitions != null)
            {
                var seen = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);

                foreach (var column in node.Definition.ColumnDefinitions)
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
                            message: $"Column '{name}' is defined more than once in the same CREATE TABLE statement.",
                            code: "duplicate-column-definition",
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
