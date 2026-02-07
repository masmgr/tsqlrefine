using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class PreferUnicodeStringLiteralsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-unicode-string-literals",
        Description: "Encourages Unicode string literals (N'...') to avoid encoding issues, using conservative safe-mode autofixes.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: true
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        foreach (var issue in FindIssues(context))
        {
            yield return issue.Diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        if (!RuleHelpers.CanProvideFix(context, diagnostic, Metadata.RuleId))
        {
            return [];
        }

        var issue = FindIssues(context)
            .FirstOrDefault(i => i.Diagnostic.Range == diagnostic.Range);

        if (issue is null)
        {
            return [];
        }

        if (!TryCreateUnicodePrefixEdit(context.Tokens, issue.Literal, out var edit))
        {
            return [];
        }

        return [new Fix(Title: "Prefix string literal with N", Edits: [edit])];
    }

    private sealed record Issue(Diagnostic Diagnostic, StringLiteral Literal);

    private static IReadOnlyList<Issue> FindIssues(RuleContext context)
    {
        var fragment = context.Ast.Fragment;
        if (fragment is null)
        {
            return Array.Empty<Issue>();
        }

        var visitor = new PreferUnicodeStringLiteralsVisitor(context.Tokens);
        fragment.Accept(visitor);
        return visitor.Issues;
    }

    private sealed class PreferUnicodeStringLiteralsVisitor : TSqlFragmentVisitor
    {
        private readonly IReadOnlyList<Token> _tokens;
        private readonly Dictionary<string, SqlDataTypeReference> _variableTypes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<LiteralKey> _unsafeLiterals = new();
        private readonly List<Issue> _issues = new();

        public PreferUnicodeStringLiteralsVisitor(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens ?? Array.Empty<Token>();
        }

        public IReadOnlyList<Issue> Issues => _issues;

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                if (declaration.DataType is not SqlDataTypeReference sqlDataType)
                {
                    continue;
                }

                TrackVariableType(declaration.VariableName.Value, sqlDataType);
                MarkValueIfNonUnicode(declaration.Value as StringLiteral, sqlDataType);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            if (node.DataType is SqlDataTypeReference sqlDataType)
            {
                TrackVariableType(node.VariableName.Value, sqlDataType);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SetVariableStatement node)
        {
            MarkLiteralForVariable(node.Variable.Name, node.Expression as StringLiteral);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            MarkLiteralForVariable(node.Variable.Name, node.Expression as StringLiteral);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CastCall node)
        {
            if (node.DataType is SqlDataTypeReference sqlDataType &&
                SqlDataTypeHelpers.IsEncodingSensitiveType(sqlDataType))
            {
                MarkUnsafeIn(node.Parameter);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ConvertCall node)
        {
            if (node.DataType is SqlDataTypeReference sqlDataType &&
                SqlDataTypeHelpers.IsEncodingSensitiveType(sqlDataType))
            {
                MarkUnsafeIn(node.Parameter);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (node.FirstExpression is StringLiteral first && node.SecondExpression is not StringLiteral)
            {
                MarkUnsafe(first);
            }

            if (node.SecondExpression is StringLiteral second && node.FirstExpression is not StringLiteral)
            {
                MarkUnsafe(second);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(LikePredicate node)
        {
            if (node.SecondExpression is StringLiteral literal)
            {
                MarkUnsafe(literal);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InPredicate node)
        {
            if (node.Values is not null)
            {
                foreach (var value in node.Values)
                {
                    MarkUnsafeIn(value);
                }
            }

            if (node.Subquery is not null)
            {
                MarkUnsafeIn(node.Subquery);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.Parameters is not null)
            {
                foreach (var param in node.Parameters)
                {
                    MarkUnsafeIn(param);
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            MarkUnsafeIn(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            MarkUnsafeIn(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            MarkUnsafeIn(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(MergeStatement node)
        {
            MarkUnsafeIn(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(StringLiteral node)
        {
            if (ShouldSkipLiteral(node))
            {
                base.ExplicitVisit(node);
                return;
            }

            var fixable = TryCreateUnicodePrefixEdit(_tokens, node, out _);

            _issues.Add(new Issue(
                Diagnostic: CreateDiagnostic(node, fixable),
                Literal: node
            ));

            base.ExplicitVisit(node);
        }

        private void MarkUnsafeIn(TSqlFragment? fragment)
        {
            if (fragment is null)
            {
                return;
            }

            var collector = new StringLiteralCollector();
            fragment.Accept(collector);

            foreach (var literal in collector.Literals)
            {
                MarkUnsafe(literal);
            }
        }

        private void MarkUnsafe(StringLiteral literal)
        {
            if (IsUnicodeLiteral(literal))
            {
                return;
            }

            _unsafeLiterals.Add(LiteralKey.From(literal));
        }

        private sealed class StringLiteralCollector : TSqlFragmentVisitor
        {
            private readonly List<StringLiteral> _literals = new();

            public IReadOnlyList<StringLiteral> Literals => _literals;

            public override void ExplicitVisit(StringLiteral node)
            {
                if (!IsUnicodeLiteral(node))
                {
                    _literals.Add(node);
                }

                base.ExplicitVisit(node);
            }
        }

        private void TrackVariableType(string variableName, SqlDataTypeReference dataType)
        {
            _variableTypes[variableName] = dataType;
        }

        private void MarkValueIfNonUnicode(StringLiteral? literal, SqlDataTypeReference dataType)
        {
            if (literal is null)
            {
                return;
            }

            if (SqlDataTypeHelpers.IsNonUnicodeStringType(dataType))
            {
                MarkUnsafe(literal);
            }
        }

        private void MarkLiteralForVariable(string variableName, StringLiteral? literal)
        {
            if (literal is null)
            {
                return;
            }

            if (_variableTypes.TryGetValue(variableName, out var dataType) &&
                SqlDataTypeHelpers.IsNonUnicodeStringType(dataType))
            {
                MarkUnsafe(literal);
            }
        }

        private bool ShouldSkipLiteral(StringLiteral literal)
        {
            return IsUnicodeLiteral(literal)
                || _unsafeLiterals.Contains(LiteralKey.From(literal));
        }

        private static Diagnostic CreateDiagnostic(StringLiteral literal, bool fixable)
        {
            return new Diagnostic(
                Range: ScriptDomHelpers.GetRange(literal),
                Message: "Prefer Unicode string literals (N'...') to avoid encoding and collation issues.",
                Code: "prefer-unicode-string-literals",
                Data: new DiagnosticData("prefer-unicode-string-literals", "Style", fixable)
            );
        }
    }

    private readonly record struct LiteralKey(int StartLine, int StartColumn, int EndLine, int EndColumn)
    {
        public static LiteralKey From(TSqlFragment fragment)
        {
            var range = ScriptDomHelpers.GetRange(fragment);
            return new LiteralKey(
                range.Start.Line,
                range.Start.Character,
                range.End.Line,
                range.End.Character);
        }
    }

    private static bool IsUnicodeLiteral(StringLiteral literal)
    {
        return literal.IsNational;
    }

    private static bool TryCreateUnicodePrefixEdit(
        IReadOnlyList<Token> tokens,
        StringLiteral literal,
        out TextEdit edit)
    {
        edit = default!;

        if (literal.IsNational || tokens.Count == 0)
        {
            return false;
        }

        var range = ScriptDomHelpers.GetRange(literal);
        var literalToken = FindLiteralToken(tokens, range);

        if (literalToken is null)
        {
            return false;
        }

        var start = literalToken.Start;
        var insertRange = new PluginSdk.Range(start, start);
        edit = new TextEdit(insertRange, "N");
        return true;
    }

    private static Token? FindLiteralToken(IReadOnlyList<Token> tokens, PluginSdk.Range range)
    {
        foreach (var token in tokens)
        {
            if (!TokenHelpers.IsPositionWithinRange(token.Start, range))
            {
                continue;
            }

            if (token.Text.Contains('\'', StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }
}
