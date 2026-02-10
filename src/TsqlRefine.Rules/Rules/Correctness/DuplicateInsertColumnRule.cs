using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects duplicate column names in INSERT column lists.
/// </summary>
public sealed class DuplicateInsertColumnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-insert-column",
        Description: "Detects duplicate column names in INSERT column lists; duplicate columns always cause a runtime error.",
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

        var visitor = new DuplicateInsertColumnVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateInsertColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            var columns = node.InsertSpecification?.Columns;
            if (columns is not null && columns.Count >= 2)
            {
                var seen = new Dictionary<string, ColumnReferenceExpression>(StringComparer.OrdinalIgnoreCase);

                foreach (var column in columns)
                {
                    var identifiers = column.MultiPartIdentifier?.Identifiers;
                    if (identifiers is null || identifiers.Count == 0)
                    {
                        continue;
                    }

                    var name = identifiers[identifiers.Count - 1].Value;
                    if (name == null)
                    {
                        continue;
                    }

                    if (seen.ContainsKey(name))
                    {
                        AddDiagnostic(
                            fragment: column,
                            message: $"Column '{name}' is specified more than once in the INSERT column list.",
                            code: "duplicate-insert-column",
                            category: "Correctness",
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
