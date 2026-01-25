using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class ReturnAfterStatementsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/return-after-statements",
        Description: "Detects unreachable statements after a RETURN statement in stored procedures or functions.",
        Category: "Correctness",
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

        var visitor = new ReturnAfterStatementsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class ReturnAfterStatementsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BeginEndBlockStatement node)
        {
            CheckStatementList(node.StatementList);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            CheckStatementList(node.StatementList);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateFunctionStatement node)
        {
            CheckStatementList(node.StatementList);
            base.ExplicitVisit(node);
        }

        private void CheckStatementList(StatementList? statementList)
        {
            if (statementList?.Statements == null || statementList.Statements.Count == 0)
            {
                return;
            }

            // Find the first RETURN statement
            int returnIndex = -1;
            for (int i = 0; i < statementList.Statements.Count; i++)
            {
                if (statementList.Statements[i] is ReturnStatement)
                {
                    returnIndex = i;
                    break;
                }
            }

            // If no RETURN or RETURN is the last statement, no problem
            if (returnIndex == -1 || returnIndex == statementList.Statements.Count - 1)
            {
                return;
            }

            // Report all statements after the RETURN as unreachable
            for (int i = returnIndex + 1; i < statementList.Statements.Count; i++)
            {
                var unreachableStatement = statementList.Statements[i];

                AddDiagnostic(
                    fragment: unreachableStatement,
                    message: "Unreachable code after RETURN statement. This statement will never be executed.",
                    code: "semantic/return-after-statements",
                    category: "Correctness",
                    fixable: false
                );
            }
        }
    }
}
