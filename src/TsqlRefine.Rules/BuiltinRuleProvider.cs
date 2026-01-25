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
            new UndefinedAliasRule(),
            new AvoidExecDynamicSqlRule(),
            new AvoidMergeRule(),
            new AvoidImplicitConversionInPredicateRule(),
            new SemicolonTerminationRule(),
            new RequireAsForTableAliasRule(),
            new RequireAsForColumnAliasRule(),
            new MeaningfulAliasRule(),
            new InsertColumnCountMismatchRule(),
            new CteNameConflictRule(),
            new ReturnAfterStatementsRule(),
            new JoinConditionAlwaysTrueRule(),
            new LeftJoinFilteredByWhereRule(),
            new AliasScopeViolationRule(),
            new PrintStatementRule(),
            new DisallowCursorsRule(),
            new ConditionalBeginEndRule(),
            new FullTextRule(),
            new DataCompressionRule(),
            new InformationSchemaRule(),
            new ObjectPropertyRule(),
            new LinkedServerRule(),
            new NamedConstraintRule(),
            new CrossDatabaseTransactionRule()
        };
}

