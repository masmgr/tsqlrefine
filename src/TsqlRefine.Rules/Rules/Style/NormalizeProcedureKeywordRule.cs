using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Rule that normalizes the abbreviated PROC keyword to its full form PROCEDURE.
/// </summary>
public sealed class NormalizeProcedureKeywordRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "normalize-procedure-keyword",
        Description: "Normalizes 'PROC' to 'PROCEDURE' for consistency.",
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

        var visitor = new ProcedureKeywordVisitor(Metadata);
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

        yield return RuleHelpers.CreateReplaceFix("Use 'PROCEDURE'", diagnostic.Range, "PROCEDURE");
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

            if (!TokenHelpers.IsKeyword(token, "PROC"))
            {
                continue;
            }

            var start = token.Start;
            var end = TokenHelpers.GetTokenEnd(token);

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: "Use 'PROCEDURE' instead of 'PROC'.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }
    }

    private sealed class ProcedureKeywordVisitor : TSqlFragmentVisitor
    {
        private readonly RuleMetadata _metadata;
        private readonly List<Diagnostic> _diagnostics = new();

        public ProcedureKeywordVisitor(RuleMetadata metadata)
        {
            _metadata = metadata;
        }

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            CollectFromStatement(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateOrAlterProcedureStatement node)
        {
            CollectFromStatement(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterProcedureStatement node)
        {
            CollectFromStatement(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropProcedureStatement node)
        {
            CollectFromStatement(node);
            base.ExplicitVisit(node);
        }

        private void CollectFromStatement(TSqlFragment node)
        {
            var tokenStream = node.ScriptTokenStream;
            if (tokenStream is null || tokenStream.Count == 0)
            {
                return;
            }

            var start = Math.Max(0, node.FirstTokenIndex);
            var end = Math.Min(node.LastTokenIndex, tokenStream.Count - 1);

            for (var i = start; i <= end; i++)
            {
                var token = tokenStream[i];
                if (token.TokenType != TSqlTokenType.Proc)
                {
                    continue;
                }

                _diagnostics.Add(CreateTokenDiagnostic(token, _metadata, "Use 'PROCEDURE' instead of 'PROC'."));
            }
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
