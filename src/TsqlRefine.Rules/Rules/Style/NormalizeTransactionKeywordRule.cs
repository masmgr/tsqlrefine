using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Rule that normalizes transaction-related keywords:
/// - TRAN → TRANSACTION
/// - COMMIT (standalone) → COMMIT TRANSACTION
/// - ROLLBACK (standalone) → ROLLBACK TRANSACTION
/// </summary>
public sealed class NormalizeTransactionKeywordRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "normalize-transaction-keyword",
        Description: "Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (TokenHelpers.IsTrivia(token))
            {
                continue;
            }

            // Check for TRAN (abbreviated form of TRANSACTION)
            if (TokenHelpers.IsKeyword(token, "TRAN"))
            {
                var start = token.Start;
                var end = TokenHelpers.GetTokenEnd(token);

                yield return new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(start, end),
                    Message: "Use 'TRANSACTION' instead of 'TRAN'.",
                    Severity: null,
                    Code: Metadata.RuleId,
                    Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                );
                continue;
            }

            // Check for standalone COMMIT (without TRANSACTION or WORK)
            if (TokenHelpers.IsKeyword(token, "COMMIT"))
            {
                if (IsStandaloneCommitOrRollback(tokens, i))
                {
                    var start = token.Start;
                    var end = TokenHelpers.GetTokenEnd(token);

                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(start, end),
                        Message: "Use 'COMMIT TRANSACTION' instead of 'COMMIT'.",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                    );
                }
                continue;
            }

            // Check for standalone ROLLBACK (without TRANSACTION or WORK)
            if (TokenHelpers.IsKeyword(token, "ROLLBACK"))
            {
                if (IsStandaloneCommitOrRollback(tokens, i))
                {
                    var start = token.Start;
                    var end = TokenHelpers.GetTokenEnd(token);

                    yield return new Diagnostic(
                        Range: new TsqlRefine.PluginSdk.Range(start, end),
                        Message: "Use 'ROLLBACK TRANSACTION' instead of 'ROLLBACK'.",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
                    );
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        var tokens = context.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            yield break;
        }

        // Find the token at the diagnostic range
        var tokenIndex = TokenHelpers.FindTokenIndexByRange(tokens, diagnostic.Range);
        if (tokenIndex < 0)
        {
            yield break;
        }

        var token = tokens[tokenIndex];

        // TRAN → TRANSACTION (simple replacement)
        if (TokenHelpers.IsKeyword(token, "TRAN"))
        {
            yield return RuleHelpers.CreateReplaceFix("Use 'TRANSACTION'", diagnostic.Range, "TRANSACTION");
            yield break;
        }

        // COMMIT → COMMIT TRANSACTION (insert " TRANSACTION" after COMMIT)
        if (TokenHelpers.IsKeyword(token, "COMMIT"))
        {
            var end = TokenHelpers.GetTokenEnd(token);
            yield return RuleHelpers.CreateInsertFix("Use 'COMMIT TRANSACTION'", end, " TRANSACTION");
            yield break;
        }

        // ROLLBACK → ROLLBACK TRANSACTION (insert " TRANSACTION" after ROLLBACK)
        if (TokenHelpers.IsKeyword(token, "ROLLBACK"))
        {
            var end = TokenHelpers.GetTokenEnd(token);
            yield return RuleHelpers.CreateInsertFix("Use 'ROLLBACK TRANSACTION'", end, " TRANSACTION");
        }
    }

    /// <summary>
    /// Checks if COMMIT or ROLLBACK is standalone (not followed by TRANSACTION, TRAN, or WORK).
    /// </summary>
    private static bool IsStandaloneCommitOrRollback(IReadOnlyList<Token> tokens, int index)
    {
        var nextIndex = TokenHelpers.GetNextNonTriviaIndex(tokens, index);
        if (nextIndex < 0)
        {
            // End of tokens - this is standalone
            return true;
        }

        var nextToken = tokens[nextIndex];

        // COMMIT/ROLLBACK TRANSACTION or COMMIT/ROLLBACK TRAN - not standalone
        if (TokenHelpers.IsKeyword(nextToken, "TRANSACTION") ||
            TokenHelpers.IsKeyword(nextToken, "TRAN"))
        {
            return false;
        }

        // COMMIT/ROLLBACK WORK - ANSI compatible syntax, not standalone (and we don't want to change it)
        if (TokenHelpers.IsKeyword(nextToken, "WORK"))
        {
            return false;
        }

        // Anything else means standalone
        return true;
    }

}
