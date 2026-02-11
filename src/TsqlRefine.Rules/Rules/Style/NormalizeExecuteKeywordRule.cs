using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Rule that normalizes the abbreviated EXEC keyword to its full form EXECUTE.
/// </summary>
public sealed class NormalizeExecuteKeywordRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "normalize-execute-keyword",
        Description: "Normalizes 'EXEC' to 'EXECUTE' for consistency.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null || context.Ast.HasErrors)
        {
            foreach (var diagnostic in AnalyzeFromTokens(context.Tokens))
            {
                yield return diagnostic;
            }

            yield break;
        }

        var visitor = new ExecuteKeywordVisitor(Metadata);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        yield return RuleHelpers.CreateReplaceFix("Use 'EXECUTE'", diagnostic.Range, "EXECUTE");
    }

    private IEnumerable<Diagnostic> AnalyzeFromTokens(IReadOnlyList<Token> tokens)
    {
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

            if (!TokenHelpers.IsKeyword(token, "EXEC"))
            {
                continue;
            }

            var start = token.Start;
            var end = TokenHelpers.GetTokenEnd(token);

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: "Use 'EXECUTE' instead of 'EXEC'.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    private sealed class ExecuteKeywordVisitor : TSqlFragmentVisitor
    {
        private readonly RuleMetadata _metadata;
        private readonly List<Diagnostic> _diagnostics = new();

        public ExecuteKeywordVisitor(RuleMetadata metadata)
        {
            _metadata = metadata;
        }

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void ExplicitVisit(ExecuteStatement node)
        {
            var tokenStream = node.ScriptTokenStream;
            if (tokenStream is null || tokenStream.Count == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            var start = Math.Max(0, node.FirstTokenIndex);
            var end = Math.Min(node.LastTokenIndex, tokenStream.Count - 1);

            for (var i = start; i <= end; i++)
            {
                var token = tokenStream[i];
                if (token.TokenType != TSqlTokenType.Exec)
                {
                    continue;
                }

                _diagnostics.Add(CreateTokenDiagnostic(token, _metadata, "Use 'EXECUTE' instead of 'EXEC'."));
            }

            base.ExplicitVisit(node);
        }

        private static Diagnostic CreateTokenDiagnostic(TSqlParserToken token, RuleMetadata metadata, string message)
        {
            var start = new Position(token.Line - 1, token.Column - 1);
            var length = token.Text?.Length ?? 0;
            var end = new Position(start.Line, start.Character + length);
            return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: message,
                Severity: null,
                Code: metadata.RuleId,
                Data: new DiagnosticData(metadata.RuleId, metadata.Category, metadata.Fixable)
            );
        }
    }
}
