using System.Collections.Frozen;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class EscapeKeywordIdentifierRule : IRule
{
    private const string RuleId = "escape-keyword-identifier";
    private const string Category = "Correctness";

    /// <summary>
    /// Keywords that indicate the following identifier is a table name.
    /// </summary>
    private static readonly FrozenSet<string> TableNameContextKeywords = FrozenSet.ToFrozenSet(
        ["FROM", "JOIN", "UPDATE", "INTO", "INSERT", "MERGE", "TABLE"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Keywords that start table-level constraints in CREATE TABLE.
    /// </summary>
    private static readonly FrozenSet<string> ConstraintStartKeywords = FrozenSet.ToFrozenSet(
        ["CONSTRAINT", "PRIMARY", "FOREIGN", "CHECK", "UNIQUE"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Keywords that follow CREATE and indicate the object type (excluding TABLE).
    /// When these are seen after CREATE, we know it's NOT CREATE TABLE.
    /// </summary>
    private static readonly FrozenSet<string> CreateObjectKeywords = FrozenSet.ToFrozenSet(
        ["FUNCTION", "PROCEDURE", "PROC", "VIEW", "INDEX", "TRIGGER",
         "DATABASE", "SCHEMA", "TYPE", "ASSEMBLY", "SEQUENCE", "SYNONYM"],
        StringComparer.OrdinalIgnoreCase);

    public RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
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

        var analyzer = new KeywordIdentifierAnalyzer(tokens);
        foreach (var keywordToken in analyzer.FindKeywordIdentifierTokens())
        {
            yield return CreateDiagnostic(keywordToken);
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            yield break;
        }

        var token = TokenHelpers.FindTokenByRange(context.Tokens, diagnostic.Range);
        if (token is null)
        {
            yield break;
        }

        yield return RuleHelpers.CreateReplaceFix("Escape keyword identifier", diagnostic.Range, $"[{token.Text}]");
    }

    private Diagnostic CreateDiagnostic(Token keywordToken)
    {
        var start = keywordToken.Start;
        var end = TokenHelpers.GetTokenEnd(keywordToken);
        var escaped = $"[{keywordToken.Text}]";

        return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(start, end),
            Message: $"Identifier '{keywordToken.Text}' is a Transact-SQL keyword. Escape it as {escaped} when used as a table or column name.",
            Severity: null,
            Code: RuleId,
            Data: new DiagnosticData(RuleId, Category, Metadata.Fixable)
        );
    }

    /// <summary>
    /// Encapsulates the state machine logic for analyzing CREATE TABLE contexts
    /// and identifying keyword identifiers that need escaping.
    /// </summary>
    private sealed class KeywordIdentifierAnalyzer(IReadOnlyList<Token> tokens)
    {
        private readonly IReadOnlyList<Token> _tokens = tokens;

        // Parenthesis tracking
        private int _parenDepth;

        // CREATE TABLE state machine
        private bool _sawCreate;
        private bool _sawCreateTable;
        private int _createTableParenDepth = -1;
        private bool _inCreateTableColumnList;
        private bool _expectingCreateTableItemStart;

        // RETURNS TABLE context (for inline table-valued functions)
        private bool _sawReturns;
        private bool _inReturnsTableContext;

        public IEnumerable<Token> FindKeywordIdentifierTokens()
        {
            for (var i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                if (TokenHelpers.IsTrivia(token))
                {
                    continue;
                }

                UpdateCreateTableState(token);

                if (TryHandleParenthesis(token))
                {
                    continue;
                }

                if (TryHandleCreateTableContext(i, out var createTableToken))
                {
                    if (createTableToken is not null)
                    {
                        yield return createTableToken;
                    }
                    continue;
                }

                if (TryFindKeywordIdentifier(i, out var keywordToken))
                {
                    yield return keywordToken!;
                }
            }
        }

        private void UpdateCreateTableState(Token token)
        {
            // Handle RETURNS keyword (for inline TVF detection)
            if (TokenHelpers.IsKeyword(token, "RETURNS"))
            {
                _sawReturns = true;
                return;
            }

            // Handle TABLE keyword
            if (TokenHelpers.IsKeyword(token, "TABLE"))
            {
                if (_sawReturns)
                {
                    // This is RETURNS TABLE - inline TVF, NOT CREATE TABLE
                    _inReturnsTableContext = true;
                    _sawReturns = false;
                    _sawCreate = false;
                    return;
                }

                if (_sawCreate)
                {
                    // This is CREATE TABLE
                    _sawCreateTable = true;
                    _sawCreate = false;
                    return;
                }

                return;
            }

            // Handle CREATE keyword
            if (TokenHelpers.IsKeyword(token, "CREATE"))
            {
                _sawCreate = true;
                _sawCreateTable = false;
                _sawReturns = false;
                _inReturnsTableContext = false;
                return;
            }

            // Handle keywords that consume CREATE (not TABLE)
            if (_sawCreate && CreateObjectKeywords.Contains(token.Text))
            {
                _sawCreate = false;
                return;
            }

            // Reset RETURNS flag if we see something other than TABLE after RETURNS
            if (_sawReturns)
            {
                _sawReturns = false;
            }
        }

        /// <summary>
        /// Handles parenthesis tokens and updates depth tracking.
        /// </summary>
        /// <returns>True if the token was a parenthesis and should be skipped.</returns>
        private bool TryHandleParenthesis(Token token)
        {
            if (token.Text == "(")
            {
                _parenDepth++;

                // Exit RETURNS TABLE context when entering function body
                if (_inReturnsTableContext)
                {
                    _inReturnsTableContext = false;
                }

                if (_sawCreateTable && !_inCreateTableColumnList)
                {
                    _inCreateTableColumnList = true;
                    _createTableParenDepth = _parenDepth;
                    _expectingCreateTableItemStart = true;
                    _sawCreateTable = false;
                }

                return true;
            }

            if (token.Text == ")")
            {
                _parenDepth = Math.Max(0, _parenDepth - 1);

                if (_inCreateTableColumnList && _parenDepth < _createTableParenDepth)
                {
                    _inCreateTableColumnList = false;
                    _createTableParenDepth = -1;
                    _expectingCreateTableItemStart = false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles tokens within CREATE TABLE column list context.
        /// </summary>
        /// <returns>True if the token was handled in CREATE TABLE context.</returns>
        private bool TryHandleCreateTableContext(int index, out Token? result)
        {
            result = null;

            if (!_inCreateTableColumnList || _parenDepth != _createTableParenDepth)
            {
                return false;
            }

            var token = _tokens[index];

            if (token.Text == ",")
            {
                _expectingCreateTableItemStart = true;
                return true;
            }

            if (_expectingCreateTableItemStart)
            {
                _expectingCreateTableItemStart = false;

                if (IsKeywordIdentifierCandidate(token) && !IsConstraintStart(index))
                {
                    result = token;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to identify a keyword used as an identifier in general SQL context.
        /// </summary>
        private bool TryFindKeywordIdentifier(int index, out Token? result)
        {
            result = null;
            var token = _tokens[index];

            if (!IsKeywordIdentifierCandidate(token))
            {
                return false;
            }

            // Skip keywords used as function/procedure names (e.g. OPENJSON(...))
            if (IsFollowedByOpenParen(index))
            {
                return false;
            }

            // Skip keywords in RETURNS TABLE context (AS, RETURN are valid keywords here)
            if (_inReturnsTableContext)
            {
                return false;
            }

            // Qualified column reference (e.g., t.order)
            if (TokenHelpers.IsPrefixedByDot(_tokens, index))
            {
                result = token;
                return true;
            }

            // Table name context (e.g., FROM order, JOIN table)
            var previousIndex = TokenHelpers.GetPreviousNonTriviaIndex(_tokens, index);
            if (previousIndex >= 0 && IsTableNameContextKeyword(_tokens[previousIndex]))
            {
                // Don't treat TABLE in RETURNS TABLE as table name context
                if (TokenHelpers.IsKeyword(_tokens[previousIndex], "TABLE"))
                {
                    var beforeTableIndex = TokenHelpers.GetPreviousNonTriviaIndex(_tokens, previousIndex);
                    if (beforeTableIndex >= 0 && TokenHelpers.IsKeyword(_tokens[beforeTableIndex], "RETURNS"))
                    {
                        return false;
                    }
                }

                // Don't flag keywords that are themselves context keywords (e.g., INTO after INSERT)
                if (IsTableNameContextKeyword(token))
                {
                    return false;
                }
                result = token;
                return true;
            }

            return false;
        }

        private static bool IsKeywordIdentifierCandidate(Token token)
        {
            if (!TokenHelpers.IsLikelyKeyword(token))
            {
                return false;
            }

            var text = token.Text;
            return !string.IsNullOrEmpty(text) && char.IsLetter(text[0]);
        }

        private static bool IsTableNameContextKeyword(Token token)
        {
            return TableNameContextKeywords.Contains(token.Text);
        }

        private bool IsFollowedByOpenParen(int index)
        {
            var nextIndex = TokenHelpers.GetNextNonTriviaIndex(_tokens, index);
            return nextIndex >= 0 && _tokens[nextIndex].Text == "(";
        }

        /// <summary>
        /// Checks if the token at the given index starts a table-level constraint
        /// (PRIMARY KEY, FOREIGN KEY, CHECK, UNIQUE, CONSTRAINT).
        /// </summary>
        private bool IsConstraintStart(int itemStartIndex)
        {
            var first = _tokens[itemStartIndex];
            var firstText = first.Text;

            if (!ConstraintStartKeywords.Contains(firstText))
            {
                return false;
            }

            // CONSTRAINT keyword always starts a constraint
            if (firstText.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var nextIndex = TokenHelpers.GetNextNonTriviaIndex(_tokens, itemStartIndex);
            if (nextIndex < 0)
            {
                return false;
            }

            var second = _tokens[nextIndex];
            var secondText = second.Text;

            return firstText.ToUpperInvariant() switch
            {
                "PRIMARY" => secondText.Equals("KEY", StringComparison.OrdinalIgnoreCase),
                "FOREIGN" => secondText.Equals("KEY", StringComparison.OrdinalIgnoreCase),
                "CHECK" => secondText == "(",
                "UNIQUE" => IsUniqueConstraintFollower(secondText),
                _ => false
            };
        }

        private static bool IsUniqueConstraintFollower(string secondText)
        {
            return secondText == "(" ||
                   secondText.Equals("CLUSTERED", StringComparison.OrdinalIgnoreCase) ||
                   secondText.Equals("NONCLUSTERED", StringComparison.OrdinalIgnoreCase) ||
                   secondText.Equals("KEY", StringComparison.OrdinalIgnoreCase);
        }
    }
}
