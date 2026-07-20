using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects deprecated TEXT, NTEXT, IMAGE, and TIMESTAMP data types.
/// </summary>
public sealed class AvoidDeprecatedTypesRule : DiagnosticVisitorRuleBase
{
    private static readonly FrozenDictionary<SqlDataTypeOption, (string TypeName, string Replacement)> s_deprecatedTypes =
        new Dictionary<SqlDataTypeOption, (string TypeName, string Replacement)>
        {
            [SqlDataTypeOption.Text] = ("TEXT", "VARCHAR(MAX)"),
            [SqlDataTypeOption.NText] = ("NTEXT", "NVARCHAR(MAX)"),
            [SqlDataTypeOption.Image] = ("IMAGE", "VARBINARY(MAX)"),
            [SqlDataTypeOption.Timestamp] = ("TIMESTAMP", "ROWVERSION"),
        }.ToFrozenDictionary();

    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-deprecated-types",
        Description: "Detects deprecated TEXT, NTEXT, IMAGE, and TIMESTAMP data types and recommends modern replacements.",
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
            if (s_deprecatedTypes.TryGetValue(node.SqlDataTypeOption, out var deprecatedType))
            {
                AddDiagnostic(
                    fragment: node,
                    message: $"Avoid deprecated '{deprecatedType.TypeName}' data type. Use '{deprecatedType.Replacement}' instead.",
                    code: "avoid-deprecated-types",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
