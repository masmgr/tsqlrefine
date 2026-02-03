using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Diagnostics;
using TsqlRefine.Rules.Helpers.Visitors;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed partial class AvoidAmbiguousDatetimeLiteralRule : IRule
{
    [GeneratedRegex(@"^\s*\d{1,2}[/]\d{1,2}[/]\d{2,4}\s*$", RegexOptions.Compiled)]
    private static partial Regex SlashDatePattern();

    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-ambiguous-datetime-literal",
        Description: "Disallows slash-delimited date literals; they depend on language/locale and can silently change meaning - prefer ISO 8601.",
        Category: "Correctness",
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

        var visitor = new AmbiguousDatetimeLiteralVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AmbiguousDatetimeLiteralVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(StringLiteral node)
        {
            var text = node.Value ?? string.Empty;
            if (SlashDatePattern().IsMatch(text))
            {
                var message = $"Avoid slash-delimited date literal '{text}'; it depends on locale settings. Use ISO 8601 format (YYYY-MM-DD) instead.";
                AddDiagnostic(node, message, "avoid-ambiguous-datetime-literal", "Correctness", false);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(Identifier node)
        {
            if (node.QuoteType == QuoteType.DoubleQuote)
            {
                var text = node.Value ?? string.Empty;
                if (SlashDatePattern().IsMatch(text))
                {
                    var message = $"Avoid slash-delimited date literal '{text}'; it depends on locale settings. Use ISO 8601 format (YYYY-MM-DD) instead.";
                    AddDiagnostic(node, message, "avoid-ambiguous-datetime-literal", "Correctness", false);
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
