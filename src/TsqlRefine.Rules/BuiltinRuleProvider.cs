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
            new CrossDatabaseTransactionRule(),
            new EscapeKeywordIdentifierRule(),
            new KeywordCapitalizationRule(),
            new DuplicateEmptyLineRule(),
            new DuplicateGoRule(),
            new NestedBlockCommentsRule(),
            new SetAnsiRule(),
            new SetNocountRule(),
            new SetQuotedIdentifierRule(),
            new SetTransactionIsolationLevelRule(),
            new CaseSensitiveVariablesRule(),
            new DataTypeLengthRule(),
            new SetVariableRule(),
            new UnicodeStringRule(),
            new SchemaQualifyRule(),
            new MultiTableAliasRule(),
            new UpperLowerRule(),
            new CountStarRule(),
            new NonSargableRule(),
            new UtcDatetimeRule(),
            new JoinKeywordRule(),
            // New rules from tsqllint-extend-rules
            new RequireBeginEndForWhileRule(),
            new RequireBeginEndForIfWithControlflowExceptionRule(),
            new DisallowSelectIntoRule(),
            new AvoidTopInDmlRule(),
            new AvoidAtatIdentityRule(),
            new AvoidMagicConvertStyleForDatetimeRule(),
            new PreferTrimOverLtrimRtrimRule(),
            new RequireExplicitJoinTypeRule(),
            new ForbidTop100PercentOrderByRule(),
            new RequirePrimaryKeyOrUniqueConstraintRule(),
            new AvoidHeapTableRule(),
            new AvoidAmbiguousDatetimeLiteralRule(),
            new PreferCoalesceOverNestedIsnullRule(),
            new PreferConcatOverPlusRule(),
            new PreferConcatOverPlusWhenNullableOrConvertRule(),
            new PreferTryConvertPatternsRule(),
            new QualifiedSelectColumnsRule(),
            new RequireQualifiedColumnsEverywhereRule(),
            new OrderByInSubqueryRule(),
            new RequireMsDescriptionForTableDefinitionFileRule(),
            new RequireTryCatchForTransactionRule(),
            new RequireXactAbortOnRule(),
            new PreferConcatWsRule(),
            new PreferStringAggOverStuffRule(),
            new PreferJsonFunctionsRule()
        };
}

