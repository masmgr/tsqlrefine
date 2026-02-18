using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects TRIM with FROM clause inside RETURN statements, which fails to parse due to a known ScriptDOM bug.
/// </summary>
public sealed class TrimFromInReturnRule : DiagnosticVisitorRuleBase
{
    // ScriptDOM bug: TRIM('x' FROM ...) inside RETURN causes parse error 46010.
    // The same syntax parses correctly in SELECT and SET statements.
    // Affected variants: TRIM(chars FROM ...), TRIM(LEADING ... FROM ...), TRIM(TRAILING ... FROM ...), TRIM(BOTH ... FROM ...)
    private const int TrimFromParseErrorNumber = 46010;

    public override RuleMetadata Metadata { get; } = new(
        RuleId: "trim-from-in-return",
        Description: "Detects TRIM with FROM clause inside RETURN statements, which fails to parse due to a known ScriptDOM bug. Workaround: assign the result to a variable first.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new TrimFromInReturnVisitor(context);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TrimFromInReturnVisitor : DiagnosticVisitorBase
    {
        public TrimFromInReturnVisitor(RuleContext context)
        {
            AddDiagnosticsFromParseErrors(context);
        }

        private void AddDiagnosticsFromParseErrors(RuleContext context)
        {
            var rawSql = context.Ast.RawSql;

            // Quick pre-check: TRIM and FROM must both appear in the SQL
            if (!rawSql.Contains("TRIM", StringComparison.OrdinalIgnoreCase) ||
                !rawSql.Contains(" FROM ", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var parseError in context.Ast.ParseErrors)
            {
                if (parseError.Number != TrimFromParseErrorNumber)
                {
                    continue;
                }

                // Check that the error location is near a TRIM keyword
                if (!IsNearTrimKeyword(rawSql, parseError.Offset))
                {
                    continue;
                }

                var startOffset = Math.Clamp(parseError.Offset, 0, rawSql.Length);
                var endOffset = Math.Clamp(startOffset + 4, 0, rawSql.Length); // "TRIM".Length
                var start = TextPositionHelpers.OffsetToPosition(rawSql, startOffset);
                var end = TextPositionHelpers.OffsetToPosition(rawSql, endOffset);

                AddDiagnostic(
                    range: new PluginSdk.Range(start, end),
                    message: "TRIM with FROM clause inside RETURN cannot be parsed due to a known ScriptDOM bug. Workaround: assign to a variable first, e.g. SET @result = TRIM(...); RETURN @result;",
                    code: "trim-from-in-return",
                    category: "Correctness",
                    fixable: false,
                    severity: DiagnosticSeverity.Warning
                );
            }
        }

        /// <summary>
        /// Checks whether the parse error offset is at or near a TRIM keyword.
        /// </summary>
        private static bool IsNearTrimKeyword(string rawSql, int errorOffset)
        {
            // The error offset points to the TRIM keyword position.
            // Check within a small window around the error offset.
            const int windowSize = 10;
            var searchStart = Math.Max(0, errorOffset - windowSize);
            var searchEnd = Math.Min(rawSql.Length, errorOffset + windowSize + 4);
            var window = rawSql[searchStart..searchEnd];
            return window.Contains("TRIM", StringComparison.OrdinalIgnoreCase);
        }
    }
}
