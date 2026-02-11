using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects deprecated TEXT, NTEXT, and IMAGE data types. Use VARCHAR(MAX), NVARCHAR(MAX), or VARBINARY(MAX) instead.
/// </summary>
public sealed class AvoidDeprecatedTypesRule : DiagnosticVisitorRuleBase
{
    private static readonly FrozenDictionary<SqlDataTypeOption, string> s_deprecatedTypes =
        new Dictionary<SqlDataTypeOption, string>
        {
            [SqlDataTypeOption.Text] = "VARCHAR(MAX)",
            [SqlDataTypeOption.NText] = "NVARCHAR(MAX)",
            [SqlDataTypeOption.Image] = "VARBINARY(MAX)",
        }.ToFrozenDictionary();

    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-deprecated-types",
        Description: "Detects deprecated TEXT, NTEXT, and IMAGE data types. Use VARCHAR(MAX), NVARCHAR(MAX), or VARBINARY(MAX) instead.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidDeprecatedTypesVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidDeprecatedTypesVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SqlDataTypeReference node)
        {
            if (s_deprecatedTypes.TryGetValue(node.SqlDataTypeOption, out var replacement))
            {
                var typeName = node.SqlDataTypeOption.ToString().ToUpperInvariant();
                AddDiagnostic(
                    fragment: node,
                    message: $"Avoid deprecated '{typeName}' data type. Use '{replacement}' instead.",
                    code: "avoid-deprecated-types",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
