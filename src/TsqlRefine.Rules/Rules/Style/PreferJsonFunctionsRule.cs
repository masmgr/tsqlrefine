using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class PreferJsonFunctionsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-json-functions",
        Description: "Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+).",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // JSON functions are available in SQL Server 2016+ (CompatLevel 130+)
        if (context.CompatLevel < 130)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new PreferJsonFunctionsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferJsonFunctionsVisitor : DiagnosticVisitorBase
    {
        private static readonly FrozenSet<string> JsonParsingFunctions = FrozenSet.ToFrozenSet(
            ["CHARINDEX", "SUBSTRING", "PATINDEX", "STUFF", "REPLACE"],
            StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(FunctionCall node)
        {
            var funcName = node.FunctionName.Value;

            // Check for string manipulation functions that might be used for JSON parsing
            if (JsonParsingFunctions.Contains(funcName))
            {
                // Heuristic: Check if parameters contain string literals that look like JSON patterns
                if (node.Parameters != null)
                {
                    foreach (var param in node.Parameters)
                    {
                        if (ContainsJsonPattern(param))
                        {
                            AddDiagnostic(
                                fragment: node,
                                message: $"Consider using built-in JSON functions (JSON_VALUE, JSON_QUERY, OPENJSON) instead of manual string parsing with {funcName}.",
                                code: "prefer-json-functions",
                                category: "Style",
                                fixable: false
                            );
                            break;
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool ContainsJsonPattern(ScalarExpression expression)
        {
            if (expression is StringLiteral strLit)
            {
                var value = strLit.Value;
                // Check for common JSON patterns
                return value.Contains('{') ||
                       value.Contains('}') ||
                       value.Contains('[') ||
                       value.Contains(']') ||
                       value.Contains("\":", StringComparison.Ordinal) ||
                       value.Contains("':", StringComparison.Ordinal);
            }

            if (expression is BinaryExpression binary)
            {
                return ContainsJsonPattern(binary.FirstExpression) || ContainsJsonPattern(binary.SecondExpression);
            }

            if (expression is FunctionCall func)
            {
                // Check for concatenation patterns building JSON
                if (func.Parameters != null)
                {
                    foreach (var param in func.Parameters)
                    {
                        if (ContainsJsonPattern(param))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
