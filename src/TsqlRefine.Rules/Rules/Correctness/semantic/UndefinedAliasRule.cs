using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

public sealed class UndefinedAliasRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/undefined-alias",
        Description: "Detects references to undefined table aliases in column qualifiers.",
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

        var visitor = new UndefinedAliasVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UndefinedAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;
            if (querySpec == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Phase 1: Collect declared aliases from FROM clause
            var declaredAliases = querySpec.FromClause != null
                ? TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Phase 2: Check column references in the entire query
            var columnRefChecker = new ColumnReferenceChecker(declaredAliases, this);

            // Check SELECT list
            if (querySpec.SelectElements != null)
            {
                foreach (var selectElement in querySpec.SelectElements)
                {
                    selectElement.Accept(columnRefChecker);
                }
            }

            // Check WHERE clause
            querySpec.WhereClause?.Accept(columnRefChecker);

            // Check GROUP BY clause
            querySpec.GroupByClause?.Accept(columnRefChecker);

            // Check HAVING clause
            querySpec.HavingClause?.Accept(columnRefChecker);

            // Check ORDER BY clause
            querySpec.OrderByClause?.Accept(columnRefChecker);

            // Check JOIN conditions using helper
            if (querySpec.FromClause != null)
            {
                foreach (var tableRef in querySpec.FromClause.TableReferences)
                {
                    TableReferenceHelpers.TraverseJoinConditions(tableRef, (join, condition) =>
                    {
                        condition.Accept(columnRefChecker);
                    });
                }
            }

            base.ExplicitVisit(node);
        }

        private sealed class ColumnReferenceChecker : ScopeBoundaryAwareVisitor
        {
            private readonly HashSet<string> _declaredAliases;
            private readonly UndefinedAliasVisitor _parent;

            public ColumnReferenceChecker(HashSet<string> declaredAliases, UndefinedAliasVisitor parent)
            {
                _declaredAliases = declaredAliases;
                _parent = parent;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                // Check if this is a qualified column reference (e.g., table.column)
                var qualifier = ColumnReferenceHelpers.GetTableQualifier(node);

                if (qualifier != null && !_declaredAliases.Contains(qualifier))
                {
                    _parent.AddDiagnostic(
                        fragment: node.MultiPartIdentifier!.Identifiers[0],
                        message: $"Undefined table alias '{qualifier}'. Table or alias '{qualifier}' is not declared in the FROM clause.",
                        code: "semantic/undefined-alias",
                        category: "Correctness",
                        fixable: false
                    );
                }

                base.ExplicitVisit(node);
            }
        }
    }
}
