using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization
/// </summary>
public sealed class DataCompressionRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-data-compression",
        Description: "Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DataCompressionVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DataCompressionVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(CreateTableStatement node)
        {
            // Check if DATA_COMPRESSION option is specified in table options
            var hasDataCompression = false;

            if (node.Options != null)
            {
                foreach (var option in node.Options)
                {
                    // Check for TableDataCompressionOption type
                    if (option.GetType().Name.Contains("Compression", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDataCompression = true;
                        break;
                    }
                }
            }

            if (!hasDataCompression)
            {
                AddDiagnostic(
                    fragment: node.SchemaObjectName.BaseIdentifier,
                    message: "CREATE TABLE statement missing DATA_COMPRESSION option. Consider specifying ROW, PAGE, or NONE for optimal storage.",
                    code: "require-data-compression",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
