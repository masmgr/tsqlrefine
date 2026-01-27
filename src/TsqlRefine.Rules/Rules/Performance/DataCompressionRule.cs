using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class DataCompressionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "data-compression",
        Description: "Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization",
        Category: "Performance",
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

        var visitor = new DataCompressionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
                    fragment: node,
                    message: "CREATE TABLE statement missing DATA_COMPRESSION option. Consider specifying ROW, PAGE, or NONE for optimal storage.",
                    code: "data-compression",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
