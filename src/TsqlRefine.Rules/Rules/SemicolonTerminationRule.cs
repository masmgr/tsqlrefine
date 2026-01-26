using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

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
                    var range = ScriptDomHelpers.GetRange(statement);
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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (context.Ast.Fragment is null || context.Ast.Fragment is not TSqlScript script)
        {
            return Array.Empty<Fix>();
        }

        if (script.ScriptTokenStream is null || script.ScriptTokenStream.Count == 0)
        {
            return Array.Empty<Fix>();
        }

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                var range = ScriptDomHelpers.GetRange(statement);
                if (range != diagnostic.Range)
                {
                    continue;
                }

                if (HasSemicolonTerminator(statement, script.ScriptTokenStream))
                {
                    return Array.Empty<Fix>();
                }

                var insertAt = range.End;
                var insertRange = new TsqlRefine.PluginSdk.Range(insertAt, insertAt);

                return new[]
                {
                    new Fix(
                        Title: "Insert semicolon",
                        Edits: new[] { new TextEdit(insertRange, ";") }
                    )
                };
            }
        }

        return Array.Empty<Fix>();
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
}
