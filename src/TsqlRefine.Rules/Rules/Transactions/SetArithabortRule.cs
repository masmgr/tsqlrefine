using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET ARITHABORT ON within the first 10 statements.
/// </summary>
public sealed class SetArithabortRule() : SetOptionPreambleRuleBase(
    "set-arithabort", "ARITHABORT", RuleSeverity.Warning, SetOptions.ArithAbort);
