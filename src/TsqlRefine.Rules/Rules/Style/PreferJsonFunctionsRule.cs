using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+).
/// </summary>
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

            if (JsonParsingFunctions.Contains(funcName) && node.Parameters != null)
            {
                if (HasJsonEvidence(node.Parameters))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Consider using built-in JSON functions (JSON_VALUE, JSON_QUERY, OPENJSON) instead of manual string parsing with {funcName}.",
                        code: "prefer-json-functions",
                        category: "Style",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        /// <summary>
        /// Checks whether the function parameters collectively contain evidence of JSON string manipulation.
        /// Uses a two-tier approach: strong multi-character JSON patterns, then paired brace detection across parameters.
        /// </summary>
        private static bool HasJsonEvidence(IList<ScalarExpression> parameters)
        {
            var literals = new List<string>();
            foreach (var param in parameters)
            {
                CollectStringLiterals(param, literals);
            }

            if (literals.Count == 0)
            {
                return false;
            }

            // Tier 1: Any single literal contains a strong multi-character JSON pattern
            foreach (var value in literals)
            {
                if (IsStrongJsonLiteral(value))
                {
                    return true;
                }
            }

            // Tier 2: Paired braces across all literals suggest JSON object boundary detection
            var hasOpenBrace = false;
            var hasCloseBrace = false;

            foreach (var value in literals)
            {
                if (value.Contains('{')) hasOpenBrace = true;
                if (value.Contains('}')) hasCloseBrace = true;
            }

            if (hasOpenBrace && hasCloseBrace)
            {
                return true;
            }

            // Tier 3: Paired brackets, excluding PATINDEX/LIKE character class patterns
            var hasOpenBracket = false;
            var hasCloseBracket = false;

            foreach (var value in literals)
            {
                if (value.Contains('[')) hasOpenBracket = true;
                if (value.Contains(']')) hasCloseBracket = true;
            }

            if (hasOpenBracket && hasCloseBracket)
            {
                foreach (var value in literals)
                {
                    if ((value.Contains('[') || value.Contains(']')) &&
                        !IsWildcardCharacterClass(value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a single string literal contains multi-character patterns that strongly indicate JSON content.
        /// </summary>
        private static bool IsStrongJsonLiteral(string value)
        {
            // JSON key-value separators
            if (value.Contains("\":", StringComparison.Ordinal) ||
                value.Contains("':", StringComparison.Ordinal))
            {
                return true;
            }

            // JSON structural patterns (multi-character)
            if (value.Contains("{\"", StringComparison.Ordinal) ||
                value.Contains("\"}", StringComparison.Ordinal) ||
                value.Contains("[{", StringComparison.Ordinal) ||
                value.Contains("}]", StringComparison.Ordinal) ||
                value.Contains("},{", StringComparison.Ordinal) ||
                value.Contains(",\"", StringComparison.Ordinal))
            {
                return true;
            }

            // Paired braces in the same literal suggest a JSON object
            if (value.Contains('{') && value.Contains('}'))
            {
                return true;
            }

            // Paired brackets in the same literal, but NOT wildcard character classes
            if (value.Contains('[') && value.Contains(']') && !IsWildcardCharacterClass(value))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Recursively collects all string literal values from an expression tree.
        /// </summary>
        private static void CollectStringLiterals(ScalarExpression expression, List<string> literals)
        {
            if (expression is StringLiteral strLit)
            {
                literals.Add(strLit.Value);
            }
            else if (expression is BinaryExpression binary)
            {
                CollectStringLiterals(binary.FirstExpression, literals);
                CollectStringLiterals(binary.SecondExpression, literals);
            }
            else if (expression is FunctionCall func && func.Parameters != null)
            {
                foreach (var param in func.Parameters)
                {
                    CollectStringLiterals(param, literals);
                }
            }
        }

        /// <summary>
        /// Determines whether bracket usage in a string literal represents a PATINDEX/LIKE
        /// wildcard character class (e.g., [^0], [a-z], [0-9]) rather than JSON brackets.
        /// </summary>
        private static bool IsWildcardCharacterClass(string value)
        {
            var pos = 0;
            var foundBracketPair = false;

            while (pos < value.Length)
            {
                var openBracket = value.IndexOf('[', pos);
                if (openBracket < 0) break;

                var closeBracket = value.IndexOf(']', openBracket + 1);
                if (closeBracket < 0) break;

                foundBracketPair = true;
                var content = value.AsSpan(openBracket + 1, closeBracket - openBracket - 1);

                // If content between brackets contains JSON-structural characters,
                // this is likely JSON, not a character class
                foreach (var ch in content)
                {
                    if (ch is '"' or ':' or '{' or '}' or ',')
                    {
                        return false;
                    }
                }

                pos = closeBracket + 1;
            }

            return foundBracketPair;
        }
    }
}
