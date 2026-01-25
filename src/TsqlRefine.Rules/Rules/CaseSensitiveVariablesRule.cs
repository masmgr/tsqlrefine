using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class CaseSensitiveVariablesRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/case-sensitive-variables",
        Description: "Ensures variable references match the exact casing used in their declarations for consistency.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new CaseSensitiveVariablesVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CaseSensitiveVariablesVisitor : DiagnosticVisitorBase
    {
        // Track declared variables with their exact casing (key is lowercase for lookup)
        private readonly Dictionary<string, VariableDeclaration> _declaredVariables = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            // Collect all variable declarations
            foreach (var declaration in node.Declarations)
            {
                var varName = declaration.VariableName.Value;
                if (!_declaredVariables.ContainsKey(varName))
                {
                    _declaredVariables[varName] = new VariableDeclaration(
                        varName,
                        declaration.VariableName
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            // Track stored procedure parameters
            var varName = node.VariableName.Value;
            if (!_declaredVariables.ContainsKey(varName))
            {
                _declaredVariables[varName] = new VariableDeclaration(
                    varName,
                    node.VariableName
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(VariableReference node)
        {
            CheckVariableCasing(node.Name, node);
            base.ExplicitVisit(node);
        }

        private void CheckVariableCasing(string varName, TSqlFragment fragment)
        {
            // Check if this variable was declared
            if (_declaredVariables.TryGetValue(varName, out var declaration))
            {
                // Check if the casing matches exactly
                if (varName != declaration.ExactName)
                {
                    AddDiagnostic(
                        fragment: fragment,
                        message: $"Variable reference '{varName}' does not match declared casing '{declaration.ExactName}'. Use consistent casing for better code readability.",
                        code: "semantic/case-sensitive-variables",
                        category: "Style",
                        fixable: false
                    );
                }
            }
        }

        private sealed record VariableDeclaration(
            string ExactName,
            Identifier NameIdentifier
        );
    }
}
