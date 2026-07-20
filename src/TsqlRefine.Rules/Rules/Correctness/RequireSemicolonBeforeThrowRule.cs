using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Requires a semicolon between a preceding statement and THROW.
/// </summary>
public sealed class RequireSemicolonBeforeThrowRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-semicolon-before-throw",
        Description: "Requires the statement immediately before THROW to be terminated with a semicolon.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireSemicolonBeforeThrowVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireSemicolonBeforeThrowVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(TSqlBatch node)
        {
            CheckStatementSequence(node.Statements);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(StatementList node)
        {
            CheckStatementSequence(node.Statements);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            // Without a separator, `ROLLBACK TRANSACTION THROW;` is parsed as a rollback
            // to a transaction/savepoint named THROW, so no ThrowStatement exists in the AST.
            if (node.Name?.Identifier?.Value.Equals("THROW", StringComparison.OrdinalIgnoreCase) == true)
            {
                AddDiagnostic(node.Name.Identifier);
            }

            base.ExplicitVisit(node);
        }

        private void CheckStatementSequence(IList<TSqlStatement> statements)
        {
            for (var i = 1; i < statements.Count; i++)
            {
                if (statements[i] is ThrowStatement && !HasSemicolonTerminator(statements[i - 1]))
                {
                    AddDiagnostic(statements[i]);
                }
            }
        }

        private void AddDiagnostic(TSqlFragment throwFragment)
        {
            AddDiagnostic(
                range: ScriptDomHelpers.GetFirstTokenRange(throwFragment),
                message: "Terminate the statement immediately before THROW with a semicolon; otherwise THROW can be interpreted as part of the preceding statement.",
                code: "require-semicolon-before-throw",
                category: "Correctness",
                fixable: false
            );
        }

        private static bool HasSemicolonTerminator(TSqlStatement statement)
        {
            var tokens = statement.ScriptTokenStream;
            return tokens is not null &&
                statement.LastTokenIndex >= 0 &&
                statement.LastTokenIndex < tokens.Count &&
                tokens[statement.LastTokenIndex].TokenType == TSqlTokenType.Semicolon;
        }
    }
}
