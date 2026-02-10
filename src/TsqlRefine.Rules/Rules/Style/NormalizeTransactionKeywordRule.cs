using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Rule that normalizes transaction-related keywords:
/// - TRAN → TRANSACTION
/// - COMMIT (standalone) → COMMIT TRANSACTION
/// - ROLLBACK (standalone) → ROLLBACK TRANSACTION
/// </summary>
public sealed class NormalizeTransactionKeywordRule : DiagnosticVisitorRuleBase<TSqlScript>
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "normalize-transaction-keyword",
        Description: "Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    private const string TranMessage = "Use 'TRANSACTION' instead of 'TRAN'.";
    private const string CommitMessage = "Use 'COMMIT TRANSACTION' instead of 'COMMIT'.";
    private const string RollbackMessage = "Use 'ROLLBACK TRANSACTION' instead of 'ROLLBACK'.";

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context, TSqlScript script) =>
        script.ScriptTokenStream is null
            ? new NoOpDiagnosticVisitor()
            : new TransactionKeywordVisitor(script.ScriptTokenStream, Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        if (string.Equals(diagnostic.Message, TranMessage, StringComparison.Ordinal))
        {
            yield return RuleHelpers.CreateReplaceFix("Use 'TRANSACTION'", diagnostic.Range, "TRANSACTION");
            yield break;
        }

        if (string.Equals(diagnostic.Message, CommitMessage, StringComparison.Ordinal))
        {
            yield return RuleHelpers.CreateInsertFix("Use 'COMMIT TRANSACTION'", diagnostic.Range.End, " TRANSACTION");
            yield break;
        }

        if (string.Equals(diagnostic.Message, RollbackMessage, StringComparison.Ordinal))
        {
            yield return RuleHelpers.CreateInsertFix("Use 'ROLLBACK TRANSACTION'", diagnostic.Range.End, " TRANSACTION");
        }
    }

    private sealed class TransactionKeywordVisitor : DiagnosticVisitorBase
    {
        private readonly IList<TSqlParserToken> _tokenStream;
        private readonly RuleMetadata _metadata;

        public TransactionKeywordVisitor(IList<TSqlParserToken> tokenStream, RuleMetadata metadata)
        {
            _tokenStream = tokenStream;
            _metadata = metadata;
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            CheckForTranKeyword(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SaveTransactionStatement node)
        {
            CheckForTranKeyword(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            CheckForTranKeyword(node);
            CheckStandaloneCommitOrRollback(node, "COMMIT", CommitMessage);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            CheckForTranKeyword(node);
            CheckStandaloneCommitOrRollback(node, "ROLLBACK", RollbackMessage);
            base.ExplicitVisit(node);
        }

        private void CheckForTranKeyword(TSqlFragment fragment)
        {
            if (!TryGetTokenSpan(fragment, out var startIndex, out var endIndex))
            {
                return;
            }

            for (var i = startIndex; i <= endIndex; i++)
            {
                var token = _tokenStream[i];
                if (IsTranKeyword(token))
                {
                    AddTokenDiagnostic(token, TranMessage);
                }
            }
        }

        private void CheckStandaloneCommitOrRollback(TSqlFragment fragment, string keyword, string message)
        {
            if (!TryGetTokenSpan(fragment, out var startIndex, out var endIndex))
            {
                return;
            }

            var keywordIndex = FindKeywordIndex(startIndex, endIndex, keyword);
            if (keywordIndex < 0)
            {
                return;
            }

            if (HasTransactionQualifier(keywordIndex, endIndex))
            {
                return;
            }

            AddTokenDiagnostic(_tokenStream[keywordIndex], message);
        }

        private int FindKeywordIndex(int startIndex, int endIndex, string keyword)
        {
            for (var i = startIndex; i <= endIndex; i++)
            {
                var token = _tokenStream[i];
                if (IsTrivia(token))
                {
                    continue;
                }

                if (IsKeyword(token, keyword))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool HasTransactionQualifier(int startIndex, int endIndex)
        {
            for (var i = startIndex + 1; i <= endIndex; i++)
            {
                var token = _tokenStream[i];
                if (IsTransactionKeyword(token) || IsTranKeyword(token) || IsWorkKeyword(token))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddTokenDiagnostic(TSqlParserToken token, string message)
        {
            var range = GetTokenRange(token);
            AddDiagnostic(RuleHelpers.CreateDiagnostic(range, message, _metadata.RuleId, _metadata.Category, _metadata.Fixable));
        }

        private bool TryGetTokenSpan(TSqlFragment fragment, out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            if (fragment.FirstTokenIndex < 0 || fragment.LastTokenIndex < 0)
            {
                return false;
            }

            if (fragment.FirstTokenIndex >= _tokenStream.Count || fragment.LastTokenIndex >= _tokenStream.Count)
            {
                return false;
            }

            startIndex = fragment.FirstTokenIndex;
            endIndex = fragment.LastTokenIndex;
            return true;
        }

        private static bool IsTrivia(TSqlParserToken token) =>
            token.TokenType == TSqlTokenType.WhiteSpace ||
            token.TokenType == TSqlTokenType.SingleLineComment ||
            token.TokenType == TSqlTokenType.MultilineComment;

        private static bool IsKeyword(TSqlParserToken token, string keyword)
        {
            if (IsTrivia(token))
            {
                return false;
            }

            if (token.TokenType == TSqlTokenType.Identifier ||
                token.TokenType == TSqlTokenType.QuotedIdentifier)
            {
                return false;
            }

            var text = token.Text;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return string.Equals(text, keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTranKeyword(TSqlParserToken token) => IsKeyword(token, "TRAN");

        private static bool IsTransactionKeyword(TSqlParserToken token) => IsKeyword(token, "TRANSACTION");

        private static bool IsWorkKeyword(TSqlParserToken token)
        {
            if (token.TokenType == TSqlTokenType.QuotedIdentifier)
            {
                return false;
            }

            var text = token.Text;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return string.Equals(text, "WORK", StringComparison.OrdinalIgnoreCase);
        }

        private static TsqlRefine.PluginSdk.Range GetTokenRange(TSqlParserToken token)
        {
            var start = new Position(token.Line - 1, token.Column - 1);
            var length = token.Text?.Length ?? 0;
            var end = new Position(token.Line - 1, token.Column - 1 + length);
            return new TsqlRefine.PluginSdk.Range(start, end);
        }
    }

}
