using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class SchemaQualifyRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/schema-qualify",
        Description: "Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.",
        Category: "Style",
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

        var visitor = new SchemaQualifyVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class SchemaQualifyVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(NamedTableReference node)
        {
            var schemaObject = node.SchemaObject;

            // Skip if schema is already specified
            if (schemaObject.SchemaIdentifier != null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableName = schemaObject.BaseIdentifier.Value;

            // Skip temp tables (#temp, ##global)
            if (tableName.StartsWith("#"))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip table variables (@table)
            if (tableName.StartsWith("@"))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Report unqualified table reference
            AddDiagnostic(
                fragment: schemaObject,
                message: $"Table reference '{tableName}' should include schema qualification (e.g., dbo.{tableName}) for clarity and to avoid naming conflicts.",
                code: "semantic/schema-qualify",
                category: "Style",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
