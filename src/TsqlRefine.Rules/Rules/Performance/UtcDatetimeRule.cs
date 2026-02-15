using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones
/// </summary>
public sealed class UtcDatetimeRule : IRule
{
    private static readonly FrozenSet<string> LocalDatetimeFunctions = FrozenSet.ToFrozenSet(
        ["GETDATE", "SYSDATETIME", "CURRENT_TIMESTAMP", "SYSDATETIMEOFFSET"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> UtcAlternatives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "GETDATE", "GETUTCDATE()" },
        { "SYSDATETIME", "SYSUTCDATETIME()" },
        { "CURRENT_TIMESTAMP", "GETUTCDATE()" },
        { "SYSDATETIMEOFFSET", "SYSUTCDATETIME()" }
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-utc-datetime",
        Description: "Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones",
        Category: "Performance",
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

        var visitor = new UtcDatetimeVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UtcDatetimeVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            var functionName = node.FunctionName?.Value;

            if (functionName != null && LocalDatetimeFunctions.Contains(functionName))
            {
                var utcAlternative = UtcAlternatives.TryGetValue(functionName, out var alt) ? alt : "UTC alternative";

                AddDiagnostic(
                    fragment: node,
                    message: $"Avoid using local datetime function '{functionName}'. Use {utcAlternative} instead for timezone-independent datetime handling and better consistency across distributed systems.",
                    code: "prefer-utc-datetime",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ParameterlessCall node)
        {
            if (node.ParameterlessCallType == ParameterlessCallType.CurrentTimestamp)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid using local datetime function 'CURRENT_TIMESTAMP'. Use GETUTCDATE() instead for timezone-independent datetime handling and better consistency across distributed systems.",
                    code: "prefer-utc-datetime",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

    }
}
