using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Files should start with SET CONCAT_NULL_YIELDS_NULL ON within the first 10 statements.
/// </summary>
public sealed class SetConcatNullYieldsNullRule() : SetOptionPreambleRuleBase(
    "set-concat-null-yields-null", "CONCAT_NULL_YIELDS_NULL", RuleSeverity.Warning, SetOptions.ConcatNullYieldsNull);
