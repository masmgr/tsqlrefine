using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL.
/// </summary>
public sealed class RequireMsDescriptionForTableDefinitionFileRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-ms-description-for-table-definition-file",
        Description: "Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireMsDescriptionForTableDefinitionFileVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireMsDescriptionForTableDefinitionFileVisitor : DiagnosticVisitorBase
    {
        private const string DefaultSchemaName = "dbo";
        private const char KeySeparator = '\u001F';

        private readonly List<CreateTableStatement> _tableStatements = new();
        private readonly HashSet<string> _tablesWithDescription = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _columnsWithDescription = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Skip temporary tables (#temp, ##temp)
            if (ScriptDomHelpers.IsTemporaryTableName(node.SchemaObjectName?.BaseIdentifier?.Value))
            {
                base.ExplicitVisit(node);
                return;
            }

            _tableStatements.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableProcedureReference procRef)
            {
                var procName = procRef.ProcedureReference?.ProcedureReference?.Name;
                if (procName != null)
                {
                    var fullName = string.Join(".", procName.Identifiers.Select(i => i.Value));
                    if (fullName.Equals("sp_addextendedproperty", StringComparison.OrdinalIgnoreCase) ||
                        fullName.Equals("sys.sp_addextendedproperty", StringComparison.OrdinalIgnoreCase))
                    {
                        if (procRef.Parameters != null)
                        {
                            ProcessExtendedPropertyCall(procRef.Parameters);
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void ProcessExtendedPropertyCall(IList<ExecuteParameter> parameters)
        {
            var propertyName = GetStringParameterValue(parameters, "@name", 0);
            if (!string.Equals(propertyName, "MS_Description", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var level1Type = GetStringParameterValue(parameters, "@level1type", 4);
            if (!string.Equals(level1Type, "TABLE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tableName = GetStringParameterValue(parameters, "@level1name", 5);
            var schemaName = ResolveSchemaName(parameters);
            if (tableName is null || schemaName is null)
            {
                return;
            }

            var tableKey = BuildTableKey(schemaName, tableName);
            var level2Type = GetStringParameterValue(parameters, "@level2type", 6);
            if (level2Type is null)
            {
                _tablesWithDescription.Add(tableKey);
            }
            else if (string.Equals(level2Type, "COLUMN", StringComparison.OrdinalIgnoreCase))
            {
                var columnName = GetStringParameterValue(parameters, "@level2name", 7);
                if (columnName != null)
                {
                    _columnsWithDescription.Add(BuildColumnKey(schemaName, tableName, columnName));
                }
            }
        }

        private static string? ResolveSchemaName(IList<ExecuteParameter> parameters)
        {
            var level0Type = GetStringParameterValue(parameters, "@level0type", 2);
            var level0Name = GetStringParameterValue(parameters, "@level0name", 3);

            if (level0Type is null && level0Name is null)
            {
                return DefaultSchemaName;
            }

            if (!string.Equals(level0Type, "SCHEMA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(level0Name))
            {
                return null;
            }

            return level0Name;
        }

        private static string? GetStringParameterValue(
            IList<ExecuteParameter> parameters,
            string namedParameterName,
            int positionalIndex)
        {
            foreach (var param in parameters)
            {
                if (param.Variable != null &&
                    param.Variable.Name.Equals(namedParameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return (param.ParameterValue as StringLiteral)?.Value;
                }
            }

            if (positionalIndex >= 0 && positionalIndex < parameters.Count)
            {
                var param = parameters[positionalIndex];
                if (param.Variable is null && param.ParameterValue is StringLiteral strLiteral)
                {
                    return strLiteral.Value;
                }
            }

            return null;
        }

        public override void ExplicitVisit(TSqlScript node)
        {
            base.ExplicitVisit(node);

            foreach (var tableStmt in _tableStatements)
            {
                var schemaName = tableStmt.SchemaObjectName.SchemaIdentifier?.Value ?? DefaultSchemaName;
                var tableName = tableStmt.SchemaObjectName.BaseIdentifier.Value;
                var tableKey = BuildTableKey(schemaName, tableName);
                if (!_tablesWithDescription.Contains(tableKey))
                {
                    AddDiagnostic(
                        fragment: tableStmt.SchemaObjectName.BaseIdentifier,
                        message: $"Table '{tableName}' should have an MS_Description extended property to document its purpose.",
                        code: "require-ms-description-for-table-definition-file",
                        category: "Schema",
                        fixable: false
                    );
                }

                if (tableStmt.Definition?.ColumnDefinitions != null)
                {
                    foreach (var column in tableStmt.Definition.ColumnDefinitions)
                    {
                        var columnName = column.ColumnIdentifier?.Value;
                        if (columnName != null &&
                            !_columnsWithDescription.Contains(BuildColumnKey(schemaName, tableName, columnName)))
                        {
                            AddDiagnostic(
                                fragment: column.ColumnIdentifier!,
                                message: $"Column '{columnName}' in table '{tableName}' should have an MS_Description extended property.",
                                code: "require-ms-description-for-table-definition-file",
                                category: "Schema",
                                fixable: false
                            );
                        }
                    }
                }
            }
        }

        private static string BuildTableKey(string schemaName, string tableName) =>
            string.Concat(schemaName, KeySeparator, tableName);

        private static string BuildColumnKey(string schemaName, string tableName, string columnName) =>
            string.Concat(schemaName, KeySeparator, tableName, KeySeparator, columnName);
    }
}
