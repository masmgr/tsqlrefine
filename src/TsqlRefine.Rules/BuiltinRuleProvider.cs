using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Rules;

public sealed class BuiltinRuleProvider : IRuleProvider
{
    public string Name => "tsqlrefine.builtin";

    public int PluginApiVersion => PluginApi.CurrentVersion;

    public IReadOnlyList<IRule> GetRules() =>
        new IRule[]
        {
            new AvoidSelectStarRule(),
            new DmlWithoutWhereRule(),
            new AvoidNullComparisonRule(),
            new RequireParenthesesForMixedAndOrRule(),
            new AvoidNolockRule(),
            new RequireColumnListForInsertValuesRule(),
            new RequireColumnListForInsertSelectRule(),
            new TopWithoutOrderByRule(),
            new DuplicateAliasRule(),
            new UndefinedAliasRule()
        };
}

