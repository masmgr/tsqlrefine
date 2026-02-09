using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects duplicate columns within a single FOREIGN KEY constraint definition.
/// </summary>
public sealed class DuplicateForeignKeyColumnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-foreign-key-column",
        Description: "Detects duplicate columns within a single FOREIGN KEY constraint definition.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new DuplicateForeignKeyColumnVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateForeignKeyColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            if (node.Definition?.TableConstraints == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            foreach (var constraint in node.Definition.TableConstraints)
            {
                if (constraint is ForeignKeyConstraintDefinition fk)
                {
                    var constraintName = fk.ConstraintIdentifier?.Value;
                    var display = constraintName != null
                        ? $"FOREIGN KEY constraint '{constraintName}'"
                        : "FOREIGN KEY constraint";

                    CheckColumnsForDuplicates(fk.Columns, display);
                }
            }

            base.ExplicitVisit(node);
        }

        private void CheckColumnsForDuplicates(IList<Identifier>? columns, string constraintLabel)
        {
            if (columns == null || columns.Count < 2)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in columns)
            {
                var colName = col.Value;
                if (colName == null)
                {
                    continue;
                }

                if (!seen.Add(colName))
                {
                    AddDiagnostic(
                        fragment: col,
                        message: $"Column '{colName}' is specified more than once in {constraintLabel}.",
                        code: "duplicate-foreign-key-column",
                        category: "Schema",
                        fixable: false
                    );
                }
            }
        }
    }
}
