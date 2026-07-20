using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET ANSI_PADDING ON within the first 10 statements.
/// </summary>
public sealed class SetAnsiPaddingRule() : SetOptionPreambleRuleBase(
    "set-ansi-padding", "ANSI_PADDING", RuleSeverity.Warning, SetOptions.AnsiPadding);
