using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Base class for rules that require a SET option to be enabled near the start of a file.
/// </summary>
public abstract class SetOptionPreambleRuleBase : IRule
{
    private const int MaxStatementsToCheck = 10;
    private readonly Func<TSqlStatement, bool> _matchesEnabledOption;
    private readonly string _diagnosticMessage;

    protected SetOptionPreambleRuleBase(
        string ruleId,
        string optionDisplayName,
        RuleSeverity defaultSeverity,
        SetOptions setOption)
        : this(
            ruleId,
            $"SET {optionDisplayName} ON",
            defaultSeverity,
            statement => statement is PredicateSetStatement { IsOn: true } setStatement &&
                (setStatement.Options & setOption) == setOption)
    {
    }

    protected SetOptionPreambleRuleBase(
        string ruleId,
        string requirementDisplayName,
        RuleSeverity defaultSeverity,
        Func<TSqlStatement, bool> matchesEnabledOption)
    {
        Metadata = new RuleMetadata(
            RuleId: ruleId,
            Description: $"Files should start with {requirementDisplayName} within the first 10 statements.",
            Category: "Transactions",
            DefaultSeverity: defaultSeverity,
            Fixable: false);
        _diagnosticMessage = $"File should start with '{requirementDisplayName}' within the first 10 statements.";
        _matchesEnabledOption = matchesEnabledOption;
    }

    public RuleMetadata Metadata { get; }

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is not TSqlScript script ||
            !ScriptStatementAnalysisHelpers.ShouldEnforcePreambleChecks(script))
        {
            yield break;
        }

        if (!ScriptStatementAnalysisHelpers.AnyInFirstStatements(
                script,
                MaxStatementsToCheck,
                _matchesEnabledOption))
        {
            yield return ScriptStatementAnalysisHelpers.CreateFileStartDiagnostic(
                Metadata,
                _diagnosticMessage);
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
