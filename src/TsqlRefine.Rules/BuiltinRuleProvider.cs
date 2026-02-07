using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Rules.Correctness.Semantic;
using TsqlRefine.Rules.Rules.Debug;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Rules.Safety;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Rules.Style.Semantic;
using TsqlRefine.Rules.Rules.Transactions;

namespace TsqlRefine.Rules;

public sealed class BuiltinRuleProvider : IRuleProvider
{
    private static readonly Lazy<IRule[]> s_rules = new(CreateRules);

    public string Name => "tsqlrefine.builtin";

    public int PluginApiVersion => PluginApi.CurrentVersion;

    public IReadOnlyList<IRule> GetRules() => s_rules.Value;

    private static IRule[] CreateRules() =>
    [
        // === Correctness ===
        new AvoidNullComparisonRule(),
        new RequireParenthesesForMixedAndOrRule(),
        new InsertColumnCountMismatchRule(),
        new InsertSelectColumnNameMismatchRule(),
        new CteNameConflictRule(),
        new JoinConditionAlwaysTrueRule(),
        new AliasScopeViolationRule(),
        new CaseSensitiveVariablesRule(),
        new DataTypeLengthRule(),
        new AvoidAmbiguousDatetimeLiteralRule(),
        new AvoidFloatForDecimalRule(),
        new UnreachableCaseWhenRule(),
        new UnionTypeMismatchRule(),

        // === Correctness (Semantic) ===
        new DuplicateAliasRule(),
        new UndefinedAliasRule(),
        new JoinTableNotReferencedInOnRule(),

        // === Performance ===
        new AvoidSelectStarRule(),
        new TopWithoutOrderByRule(),
        new AvoidImplicitConversionInPredicateRule(),
        new NonSargableRule(),
        new DisallowSelectDistinctRule(),
        new LikeLeadingWildcardRule(),
        new PreferExistsOverInSubqueryRule(),

        // === Safety ===
        new DmlWithoutWhereRule(),
        new AvoidMergeRule(),
        new ReturnAfterStatementsRule(),
        new LeftJoinFilteredByWhereRule(),
        new DangerousDdlRule(),
        new AvoidTopInDmlRule(),
        new NoTopWithoutOrderByInSelectIntoRule(),
        new RequireDropIfExistsRule(),

        // === Security ===
        new AvoidExecDynamicSqlRule(),
        new AvoidDangerousProceduresRule(),
        new AvoidOpenrowsetOpendatasourceRule(),
        new AvoidNolockRule(),
        new LinkedServerRule(),
        new AvoidExecuteAsRule(),

        // === Schema ===
        new NamedConstraintRule(),
        new RequirePrimaryKeyOrUniqueConstraintRule(),
        new AvoidHeapTableRule(),
        new RequireMsDescriptionForTableDefinitionFileRule(),

        // === Style ===
        new SemicolonTerminationRule(),
        new RequireAsForTableAliasRule(),
        new RequireAsForColumnAliasRule(),
        new ConditionalBeginEndRule(),
        new EscapeKeywordIdentifierRule(),
        new DuplicateEmptyLineRule(),
        new DuplicateGoRule(),
        new NestedBlockCommentsRule(),
        new SchemaQualifyRule(),
        new JoinKeywordRule(),
        new RequireBeginEndForWhileRule(),
        new RequireBeginEndForIfWithControlflowExceptionRule(),
        new RequireExplicitJoinTypeRule(),
        new BanLegacyJoinSyntaxRule(),
        new NormalizeExecuteKeywordRule(),
        new NormalizeProcedureKeywordRule(),
        new NormalizeTransactionKeywordRule(),

        // === Style (Semantic) ===
        new MultiTableAliasRule(),
        new QualifiedSelectColumnsRule(),
        new RequireQualifiedColumnsEverywhereRule(),

        // === Style (Preferences) ===
        new PreferUnicodeStringLiteralsRule(),
        new UnicodeStringRule(),
        new PreferTrimOverLtrimRtrimRule(),
        new PreferCoalesceOverNestedIsnullRule(),
        new PreferConcatOverPlusRule(),
        new PreferConcatOverPlusWhenNullableOrConvertRule(),
        new PreferTryConvertPatternsRule(),
        new PreferConcatWsRule(),
        new PreferStringAggOverStuffRule(),
        new PreferJsonFunctionsRule(),
        new UpperLowerRule(),
        new UtcDatetimeRule(),

        // === Transactions ===
        new CrossDatabaseTransactionRule(),
        new RequireTryCatchForTransactionRule(),
        new RequireXactAbortOnRule(),
        new TransactionWithoutCommitOrRollbackRule(),
        new UncommittedTransactionRule(),
        new CatchSwallowingRule(),
        new RequireSaveTransactionInNestedRule(),

        // === SET Options ===
        new SetAnsiRule(),
        new SetNocountRule(),
        new SetQuotedIdentifierRule(),
        new SetTransactionIsolationLevelRule(),
        new SetVariableRule(),

        // === Feature Usage ===
        new RequireColumnListForInsertValuesRule(),
        new RequireColumnListForInsertSelectRule(),
        new DisallowCursorsRule(),
        new FullTextRule(),
        new DataCompressionRule(),
        new InformationSchemaRule(),
        new ObjectPropertyRule(),
        new DisallowSelectIntoRule(),
        new AvoidAtatIdentityRule(),
        new AvoidMagicConvertStyleForDatetimeRule(),
        new ForbidTop100PercentOrderByRule(),
        new OrderByInSubqueryRule(),
        new StuffWithoutOrderByRule(),
        new StringAggWithoutOrderByRule(),
        new BanQueryHintsRule(),

        // === Debug ===
        new PrintStatementRule()
    ];
}

