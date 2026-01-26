using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class RequireMsDescriptionForTableDefinitionFileRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-ms-description-for-table-definition-file",
        Description: "Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL.",
        Category: "Schema Design",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new RequireMsDescriptionForTableDefinitionFileVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireMsDescriptionForTableDefinitionFileVisitor : DiagnosticVisitorBase
    {
        private readonly List<CreateTableStatement> _tableStatements = new();
        private readonly HashSet<string> _tablesWithDescription = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(CreateTableStatement node)
        {
            _tableStatements.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            // Check for sp_addextendedproperty calls
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference procRef)
            {
                var procName = procRef.ProcedureReference?.ProcedureReference?.Name;
                if (procName != null)
                {
                    var fullName = string.Join(".", procName.Identifiers.Select(i => i.Value));
                    if (fullName.Equals("sp_addextendedproperty", StringComparison.OrdinalIgnoreCase) ||
                        fullName.Equals("sys.sp_addextendedproperty", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if it's for MS_Description
                        if (procRef.Parameters != null)
                        {
                            foreach (var param in procRef.Parameters)
                            {
                                if (param.ParameterValue is StringLiteral strLiteral)
                                {
                                    if (strLiteral.Value.Equals("MS_Description", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Try to find the table name from parameters
                                        ExtractTableNameFromExtendedProperty(procRef.Parameters);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void ExtractTableNameFromExtendedProperty(IList<ExecuteParameter> parameters)
        {
            // Look for @level1name parameter which typically contains the table name
            foreach (var param in parameters)
            {
                if (param.Variable != null &&
                    param.Variable.Name.Equals("@level1name", StringComparison.OrdinalIgnoreCase))
                {
                    if (param.ParameterValue is StringLiteral strLiteral)
                    {
                        _tablesWithDescription.Add(strLiteral.Value);
                    }
                }
            }
        }

        public override void Visit(TSqlFragment node)
        {
            base.Visit(node);

            // After visiting all nodes, check if tables have descriptions
            if (node is TSqlScript)
            {
                foreach (var tableStmt in _tableStatements)
                {
                    var tableName = tableStmt.SchemaObjectName.BaseIdentifier.Value;
                    if (!_tablesWithDescription.Contains(tableName))
                    {
                        AddDiagnostic(
                            fragment: tableStmt,
                            message: $"Table '{tableName}' should have an MS_Description extended property to document its purpose.",
                            code: "require-ms-description-for-table-definition-file",
                            category: "Schema Design",
                            fixable: false
                        );
                    }
                }
            }
        }
    }
}
