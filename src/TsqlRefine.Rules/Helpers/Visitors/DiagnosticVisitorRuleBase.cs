using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base class for rules implemented by traversing AST with a diagnostic visitor.
/// </summary>
public abstract class DiagnosticVisitorRuleBase : DiagnosticVisitorRuleBase<TSqlFragment>
{
    /// <summary>
    /// Creates a visitor for the current rule execution.
    /// </summary>
    protected abstract DiagnosticVisitorBase CreateVisitor(RuleContext context);

    /// <summary>
    /// Creates a visitor for the current rule execution.
    /// </summary>
    protected sealed override DiagnosticVisitorBase CreateVisitor(RuleContext context, TSqlFragment fragment) =>
        CreateVisitor(context);
}

/// <summary>
/// Base class for visitor-driven rules that require a specific root fragment type.
/// </summary>
/// <typeparam name="TFragment">Root AST fragment type required by the rule.</typeparam>
public abstract class DiagnosticVisitorRuleBase<TFragment> : IRule
    where TFragment : TSqlFragment
{
    /// <inheritdoc />
    public abstract RuleMetadata Metadata { get; }

    /// <inheritdoc />
    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is not TFragment fragment)
        {
            return [];
        }

        var visitor = CreateVisitor(context, fragment);
        fragment.Accept(visitor);
        return visitor.Diagnostics;
    }

    /// <summary>
    /// Creates a visitor for the current rule execution.
    /// </summary>
    protected abstract DiagnosticVisitorBase CreateVisitor(RuleContext context, TFragment fragment);

    /// <inheritdoc />
    public virtual IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
