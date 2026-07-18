using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET ANSI_WARNINGS ON within the first 10 statements.
/// </summary>
public sealed class SetAnsiWarningsRule() : SetOptionPreambleRuleBase(
    "set-ansi-warnings", "ANSI_WARNINGS", RuleSeverity.Warning, SetOptions.AnsiWarnings);
