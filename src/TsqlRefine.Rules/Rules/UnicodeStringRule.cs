using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class UnicodeStringRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/unicode-string",
        Description: "Detects Unicode characters in string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);

        var issue = FindIssues(context)
            .FirstOrDefault(i => i.Diagnostic.Range == diagnostic.Range);

        if (issue is null)
        {
            return Array.Empty<Fix>();
        }

        if (!TryCreateTypeKeywordEdit(context.Tokens, issue.TypeFragment, issue.ReplacementKeyword, out var edit))
        {
            return Array.Empty<Fix>();
        }

        return new[]
        {
            new Fix(
                Title: $"Change data type to {issue.ReplacementKeyword.ToUpperInvariant()}",
                Edits: new[] { edit }
            )
        };
    }

    private sealed record Issue(
        Diagnostic Diagnostic,
        SqlDataTypeReference TypeFragment,
        string ReplacementKeyword
    );

    private static IReadOnlyList<Issue> FindIssues(RuleContext context)
    {
        var fragment = context.Ast.Fragment;
        if (fragment is null)
        {
            return Array.Empty<Issue>();
        }

        var visitor = new UnicodeStringVisitor(context.Tokens);
        fragment.Accept(visitor);
        return visitor.Issues;
    }

    private sealed class UnicodeStringVisitor : TSqlFragmentVisitor
    {
        private readonly IReadOnlyList<Token> _tokens;

        // Track variable names and their type fragments (case-insensitive).
        private readonly Dictionary<string, SqlDataTypeReference> _variableTypes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Issue> _issues = new();

        public UnicodeStringVisitor(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens ?? Array.Empty<Token>();
        }

        public IReadOnlyList<Issue> Issues => _issues;

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                if (declaration.DataType is SqlDataTypeReference sqlDataType)
                {
                    var varName = declaration.VariableName.Value;
                    _variableTypes[varName] = sqlDataType;
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            if (node.DataType is SqlDataTypeReference sqlDataType)
            {
                var varName = node.VariableName.Value;
                _variableTypes[varName] = sqlDataType;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SetVariableStatement node)
        {
            // Check if assigning a string literal to a VARCHAR/CHAR variable
            if (node.Expression is Literal literal && literal.LiteralType == LiteralType.String)
            {
                CheckStringAssignment(node.Variable.Name, literal);
            }

            base.ExplicitVisit(node);
        }


        private void CheckStringAssignment(string variableName, Literal literal)
        {
            // Check if this variable is VARCHAR or CHAR.
            if (!_variableTypes.TryGetValue(variableName, out var dataType))
            {
                return;
            }

            if (dataType.SqlDataTypeOption != SqlDataTypeOption.VarChar &&
                dataType.SqlDataTypeOption != SqlDataTypeOption.Char)
            {
                return;
            }

            // Check if the literal value contains Unicode characters.
            var literalValue = literal.Value;

            if (!string.IsNullOrEmpty(literalValue) && ContainsUnicodeCharacters(literalValue))
            {
                var replacementKeyword = dataType.SqlDataTypeOption == SqlDataTypeOption.VarChar
                    ? "nvarchar"
                    : "nchar";

                var fixable = TryCreateTypeKeywordEdit(_tokens, dataType, replacementKeyword, out _);

                _issues.Add(new Issue(
                    Diagnostic: new Diagnostic(
                        Range: ScriptDomHelpers.GetRange(literal),
                        Message: "String literal contains Unicode characters but is assigned to a non-Unicode variable (VARCHAR/CHAR). Use NVARCHAR/NCHAR to preserve Unicode data.",
                        Code: "semantic/unicode-string",
                        Data: new DiagnosticData("semantic/unicode-string", "Correctness", fixable)
                    ),
                    TypeFragment: dataType,
                    ReplacementKeyword: replacementKeyword
                ));
            }
        }

        private static bool ContainsUnicodeCharacters(string value)
        {
            // Check if any character is outside the ASCII range (0-127)
            return value.Any(c => c > 127);
        }
    }

    private static bool TryCreateTypeKeywordEdit(
        IReadOnlyList<Token> tokens,
        SqlDataTypeReference typeFragment,
        string replacementKeyword,
        out TextEdit edit)
    {
        edit = default!;

        if (tokens.Count == 0)
        {
            return false;
        }

        var range = ScriptDomHelpers.GetRange(typeFragment);
        var keywordToken = tokens.FirstOrDefault(t =>
            IsWithin(t.Start, range) &&
            IsTypeKeywordToken(t, typeFragment.SqlDataTypeOption));

        if (keywordToken is null)
        {
            return false;
        }

        var start = keywordToken.Start;
        var end = new Position(start.Line, start.Character + keywordToken.Length);
        var replacement = ApplyKeywordCasing(replacementKeyword, keywordToken.Text);

        edit = new TextEdit(new TsqlRefine.PluginSdk.Range(start, end), replacement);
        return true;
    }

    private static bool IsWithin(Position tokenStart, TsqlRefine.PluginSdk.Range fragmentRange)
    {
        if (tokenStart.Line < fragmentRange.Start.Line || tokenStart.Line > fragmentRange.End.Line)
        {
            return false;
        }

        if (tokenStart.Line == fragmentRange.Start.Line && tokenStart.Character < fragmentRange.Start.Character)
        {
            return false;
        }

        if (tokenStart.Line == fragmentRange.End.Line && tokenStart.Character > fragmentRange.End.Character)
        {
            return false;
        }

        return true;
    }

    private static bool IsTypeKeywordToken(Token token, SqlDataTypeOption option)
    {
        return option switch
        {
            SqlDataTypeOption.VarChar =>
                string.Equals(token.Text, "varchar", StringComparison.OrdinalIgnoreCase),
            SqlDataTypeOption.Char =>
                string.Equals(token.Text, "char", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string ApplyKeywordCasing(string replacementKeyword, string originalKeyword)
    {
        if (string.IsNullOrEmpty(originalKeyword))
        {
            return replacementKeyword;
        }

        if (originalKeyword.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return replacementKeyword.ToUpperInvariant();
        }

        if (originalKeyword.All(c => !char.IsLetter(c) || char.IsLower(c)))
        {
            return replacementKeyword.ToLowerInvariant();
        }

        return replacementKeyword.ToUpperInvariant();
    }
}
