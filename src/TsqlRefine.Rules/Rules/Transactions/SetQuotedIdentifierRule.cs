using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements.
/// </summary>
public sealed class SetQuotedIdentifierRule() : SetOptionPreambleRuleBase(
    "set-quoted-identifier", "QUOTED_IDENTIFIER", RuleSeverity.Warning, SetOptions.QuotedIdentifier);
