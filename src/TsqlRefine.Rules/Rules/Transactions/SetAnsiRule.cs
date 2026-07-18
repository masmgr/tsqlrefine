using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET ANSI_NULLS ON within the first 10 statements.
/// </summary>
public sealed class SetAnsiRule() : SetOptionPreambleRuleBase(
    "set-ansi", "ANSI_NULLS", RuleSeverity.Warning, SetOptions.AnsiNulls);
