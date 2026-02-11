using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style.Semantic;

/// <summary>
/// Ensures variable references match the exact casing used in their declarations for consistency.
/// </summary>
public sealed class CaseSensitiveVariablesRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-case-sensitive-variables",
        Description: "Ensures variable references match the exact casing used in their declarations for consistency.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new CaseSensitiveVariablesVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
                TrackVariableDeclaration(declaration.VariableName.Value, declaration.VariableName);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            // Track stored procedure parameters
            TrackVariableDeclaration(node.VariableName.Value, node.VariableName);

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
                        code: "semantic-case-sensitive-variables",
                        category: "Style",
                        fixable: false
                    );
                }
            }
        }

        private void TrackVariableDeclaration(string variableName, Identifier identifier)
        {
            _declaredVariables.TryAdd(
                variableName,
                new VariableDeclaration(variableName, identifier));
        }

        private sealed record VariableDeclaration(
            string ExactName,
            Identifier NameIdentifier
        );
    }
}
