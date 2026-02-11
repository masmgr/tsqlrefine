using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// SQL statements should be terminated with a semicolon
/// </summary>
public sealed class SemicolonTerminationRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semicolon-termination",
        Description: "SQL statements should be terminated with a semicolon",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null || context.Ast.Fragment is not TSqlScript script)
        {
            yield break;
        }

        // ScriptTokenStream is required for semicolon detection
        if (script.ScriptTokenStream is null || script.ScriptTokenStream.Count == 0)
        {
            yield break;
        }

        // Check each batch in the script
        foreach (var batch in script.Batches)
        {
            // Check each statement in the batch
            foreach (var statement in batch.Statements)
            {
                // Check if this statement is terminated with a semicolon
                if (!HasSemicolonTerminator(statement, script.ScriptTokenStream))
                {
                    var range = GetLastTokenRange(statement, script.ScriptTokenStream);
                    yield return new Diagnostic(
                        Range: range,
                        Message: "Statement should be terminated with a semicolon",
                        Severity: null,
                        Code: Metadata.RuleId,
                        Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Fixable: true)
                    );
                }
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            return [];
        }

        if (context.Ast.Fragment is null || context.Ast.Fragment is not TSqlScript script)
        {
            return [];
        }

        if (script.ScriptTokenStream is null || script.ScriptTokenStream.Count == 0)
        {
            return [];
        }

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                var range = GetLastTokenRange(statement, script.ScriptTokenStream);
                if (range != diagnostic.Range)
                {
                    continue;
                }

                if (HasSemicolonTerminator(statement, script.ScriptTokenStream))
                {
                    return [];
                }

                return [RuleHelpers.CreateInsertFix("Insert semicolon", range.End, ";")];
            }
        }

        return [];
    }

    private static bool HasSemicolonTerminator(TSqlStatement statement, IList<TSqlParserToken> tokenStream)
    {
        // The last token index points to the last token of the statement
        // If the statement has a semicolon, it will be the last token
        if (statement.LastTokenIndex < 0 || statement.LastTokenIndex >= tokenStream.Count)
        {
            return false;
        }

        var lastToken = tokenStream[statement.LastTokenIndex];
        return lastToken.TokenType == TSqlTokenType.Semicolon;
    }

    /// <summary>
    /// Returns a range covering only the last token of the statement,
    /// so the diagnostic points to where the semicolon should be appended.
    /// </summary>
    private static PluginSdk.Range GetLastTokenRange(TSqlStatement statement, IList<TSqlParserToken> tokenStream)
    {
        if (statement.LastTokenIndex >= 0 && statement.LastTokenIndex < tokenStream.Count)
        {
            var lastToken = tokenStream[statement.LastTokenIndex];
            var tokenText = lastToken.Text ?? string.Empty;
            var start = new Position(lastToken.Line - 1, lastToken.Column - 1);
            var end = new Position(lastToken.Line - 1, lastToken.Column - 1 + tokenText.Length);
            return new PluginSdk.Range(start, end);
        }

        // Fallback to full statement range
        return ScriptDomHelpers.GetRange(statement);
    }
}
