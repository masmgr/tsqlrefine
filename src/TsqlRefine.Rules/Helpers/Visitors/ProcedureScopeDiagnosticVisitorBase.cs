using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Routes every procedure-definition syntax through one scope-boundary callback so stateful
/// visitors cannot accidentally support CREATE while missing ALTER or CREATE OR ALTER.
/// </summary>
internal abstract class ProcedureScopeDiagnosticVisitorBase : DiagnosticVisitorBase
{
    public sealed override void ExplicitVisit(CreateProcedureStatement node) =>
        VisitProcedureScope(() => base.ExplicitVisit(node));

    public sealed override void ExplicitVisit(AlterProcedureStatement node) =>
        VisitProcedureScope(() => base.ExplicitVisit(node));

    public sealed override void ExplicitVisit(CreateOrAlterProcedureStatement node) =>
        VisitProcedureScope(() => base.ExplicitVisit(node));

    protected abstract void VisitProcedureScope(Action visitChildren);
}
