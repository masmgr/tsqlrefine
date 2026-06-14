using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style.Semantic;

/// <summary>
/// Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.
/// </summary>
public sealed class SchemaQualifyRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-schema-qualify",
        Description: "Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new SchemaQualifyVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class SchemaQualifyVisitor : DiagnosticVisitorBase
    {
        private readonly Stack<HashSet<string>> _cteScopes = new();

        public override void ExplicitVisit(SelectStatement node)
        {
            VisitWithCteScope(node.WithCtesAndXmlNamespaces, () => node.QueryExpression?.Accept(this));
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            VisitWithCteScope(node.WithCtesAndXmlNamespaces, () => node.InsertSpecification?.Accept(this));
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            VisitWithCteScope(node.WithCtesAndXmlNamespaces, () => node.UpdateSpecification?.Accept(this));
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            VisitWithCteScope(node.WithCtesAndXmlNamespaces, () => node.DeleteSpecification?.Accept(this));
        }

        public override void ExplicitVisit(MergeStatement node)
        {
            VisitWithCteScope(node.WithCtesAndXmlNamespaces, () => node.MergeSpecification?.Accept(this));
        }

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
            if (ScriptDomHelpers.IsTemporaryTableName(tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip table variables (@table)
            if (tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip CTE references (cannot be schema-qualified)
            if (IsInCteScope(tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Report unqualified table reference
            AddDiagnostic(
                fragment: schemaObject,
                message: $"Table reference '{tableName}' should include schema qualification (e.g., dbo.{tableName}) for clarity and to avoid naming conflicts.",
                code: "semantic-schema-qualify",
                category: "Style",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        private bool IsInCteScope(string tableName) =>
            _cteScopes.Any(scope => scope.Contains(tableName));

        private void VisitWithCteScope(WithCtesAndXmlNamespaces? withCtes, Action visitAction)
        {
            if (withCtes?.CommonTableExpressions is not { Count: > 0 } ctes)
            {
                visitAction();
                return;
            }

            _cteScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            try
            {
                foreach (var cte in ctes)
                {
                    var cteName = cte.ExpressionName?.Value;
                    if (!string.IsNullOrWhiteSpace(cteName) && _cteScopes.TryPeek(out var scope))
                    {
                        scope.Add(cteName);
                    }

                    cte.QueryExpression?.Accept(this);
                }

                visitAction();
            }
            finally
            {
                _cteScopes.Pop();
            }
        }
    }
}
