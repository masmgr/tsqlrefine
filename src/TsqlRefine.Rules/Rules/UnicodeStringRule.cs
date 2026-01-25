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
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new UnicodeStringVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnicodeStringVisitor : DiagnosticVisitorBase
    {
        // Track variable names and their data types (case-insensitive)
        private readonly Dictionary<string, SqlDataTypeOption> _variableTypes = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                if (declaration.DataType is SqlDataTypeReference sqlDataType)
                {
                    var varName = declaration.VariableName.Value;
                    _variableTypes[varName] = sqlDataType.SqlDataTypeOption;
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            if (node.DataType is SqlDataTypeReference sqlDataType)
            {
                var varName = node.VariableName.Value;
                _variableTypes[varName] = sqlDataType.SqlDataTypeOption;
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
            // Check if this variable is VARCHAR or CHAR
            if (!_variableTypes.TryGetValue(variableName, out var dataType))
            {
                return;
            }

            if (dataType != SqlDataTypeOption.VarChar && dataType != SqlDataTypeOption.Char)
            {
                return;
            }

            // Check if the literal value contains Unicode characters
            var literalValue = literal.Value;

            // If the literal starts with 'N', it's explicitly marked as Unicode - no issue
            // We need to check the original text to see if it has N prefix
            // Unfortunately, ScriptDom doesn't preserve this info, so we check the literal type
            // In ScriptDom, Unicode string literals (N'...') have LiteralType.String but we can't distinguish them
            // So we'll check if the string contains non-ASCII characters

            if (!string.IsNullOrEmpty(literalValue) && ContainsUnicodeCharacters(literalValue))
            {
                AddDiagnostic(
                    fragment: literal,
                    message: $"String literal contains Unicode characters but is assigned to a non-Unicode variable (VARCHAR/CHAR). Use NVARCHAR/NCHAR or prefix the literal with N'...' to preserve Unicode data.",
                    code: "semantic/unicode-string",
                    category: "Correctness",
                    fixable: false
                );
            }
        }

        private static bool ContainsUnicodeCharacters(string value)
        {
            // Check if any character is outside the ASCII range (0-127)
            return value.Any(c => c > 127);
        }
    }
}
