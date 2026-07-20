using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements.
/// </summary>
public sealed class SetTransactionIsolationLevelRule() : SetOptionPreambleRuleBase(
    "set-transaction-isolation-level",
    "SET TRANSACTION ISOLATION LEVEL",
    TsqlRefine.PluginSdk.RuleSeverity.Information,
    static statement => statement is SetTransactionIsolationLevelStatement);
