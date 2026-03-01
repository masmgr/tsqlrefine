using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects implicit type conversions in predicates using schema type information.
/// Only warns when the column side is implicitly converted, which may prevent index usage.
/// </summary>
public sealed class ImplicitConversionInPredicateSchemaRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "implicit-conversion-in-predicate-schema",
        Description: "Detects implicit type conversions on columns in predicates using schema type information.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new ImplicitConversionVisitor(context.Schema!);

    private sealed class ImplicitConversionVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;
        private Dictionary<ScalarExpression, SchemaTypeInfo?> _expressionTypeCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<ColumnReferenceExpression, SchemaTypeInfo?> _columnTypeCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<(ResolvedTable Table, string ColumnName), SchemaTypeInfo?> _tableColumnTypeCache =
            new(ResolvedTableComparers.TableColumnKeyComparer.Instance);
        private Dictionary<string, SchemaTypeInfo?> _unqualifiedColumnTypeCache =
            new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                var previousExpressionTypeCache = _expressionTypeCache;
                var previousColumnTypeCache = _columnTypeCache;
                var previousTableColumnTypeCache = _tableColumnTypeCache;
                var previousUnqualifiedColumnTypeCache = _unqualifiedColumnTypeCache;

                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);
                _expressionTypeCache = new Dictionary<ScalarExpression, SchemaTypeInfo?>(ReferenceEqualityComparer.Instance);
                _columnTypeCache = new Dictionary<ColumnReferenceExpression, SchemaTypeInfo?>(ReferenceEqualityComparer.Instance);
                _tableColumnTypeCache = new Dictionary<(ResolvedTable Table, string ColumnName), SchemaTypeInfo?>(ResolvedTableComparers.TableColumnKeyComparer.Instance);
                _unqualifiedColumnTypeCache = new Dictionary<string, SchemaTypeInfo?>(StringComparer.OrdinalIgnoreCase);

                node.WhereClause?.Accept(this);
                node.HavingClause?.Accept(this);
                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                _expressionTypeCache = previousExpressionTypeCache;
                _columnTypeCache = previousColumnTypeCache;
                _tableColumnTypeCache = previousTableColumnTypeCache;
                _unqualifiedColumnTypeCache = previousUnqualifiedColumnTypeCache;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (_currentAliasMap is not null)
            {
                CheckComparison(node.FirstExpression, node.SecondExpression, node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckComparison(ScalarExpression left, ScalarExpression right, TSqlFragment diagnosticTarget)
        {
            var leftType = ResolveExpressionType(left);
            var rightType = ResolveExpressionType(right);

            if (leftType is null || rightType is null)
            {
                return;
            }

            var result = TypeCompatibility.CheckComparison(leftType, rightType);

            // Only warn when a column is being converted (harms SARGability)
            switch (result)
            {
                case ImplicitConversionResult.LeftConverted when left is ColumnReferenceExpression leftCol:
                    ReportConversion(leftCol, leftType, rightType, diagnosticTarget);
                    break;
                case ImplicitConversionResult.RightConverted when right is ColumnReferenceExpression rightCol:
                    ReportConversion(rightCol, rightType, leftType, diagnosticTarget);
                    break;
                case ImplicitConversionResult.BothConverted:
                    if (left is ColumnReferenceExpression leftCol2)
                    {
                        ReportConversion(leftCol2, leftType, rightType, diagnosticTarget);
                    }

                    if (right is ColumnReferenceExpression rightCol2)
                    {
                        ReportConversion(rightCol2, rightType, leftType, diagnosticTarget);
                    }

                    break;
            }
        }

        private void ReportConversion(
            ColumnReferenceExpression colRef,
            SchemaTypeInfo fromType,
            SchemaTypeInfo toType,
            TSqlFragment diagnosticTarget)
        {
            var columnDisplay = FormatColumnReference(colRef);
            AddDiagnostic(
                fragment: diagnosticTarget,
                message: $"Implicit conversion on column '{columnDisplay}' from '{fromType.TypeName}' to '{toType.TypeName}' may prevent index usage.",
                code: "implicit-conversion-in-predicate-schema",
                category: "Schema",
                fixable: false
            );
        }

        private SchemaTypeInfo? ResolveExpressionType(ScalarExpression expression)
        {
            if (_expressionTypeCache.TryGetValue(expression, out var cached))
            {
                return cached;
            }

            var resolved = expression is ColumnReferenceExpression colRef
                ? ResolveColumnType(colRef)
                : InferLiteralType(expression);

            _expressionTypeCache[expression] = resolved;
            return resolved;
        }

        private SchemaTypeInfo? ResolveColumnType(ColumnReferenceExpression colRef)
        {
            if (_columnTypeCache.TryGetValue(colRef, out var cached))
            {
                return cached;
            }

            var resolved = ResolveColumnTypeCore(colRef);
            _columnTypeCache[colRef] = resolved;
            return resolved;
        }

        private SchemaTypeInfo? ResolveColumnTypeCore(ColumnReferenceExpression colRef)
        {
            if (_currentAliasMap is null || colRef.ColumnType == ColumnType.Wildcard)
            {
                return null;
            }

            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return null;
            }

            if (identifiers.Count >= 2)
            {
                var columnName = identifiers[identifiers.Count - 1].Value;

                if (!TryResolveQualifiedTable(identifiers, out var resolvedTable) || resolvedTable is null)
                {
                    return null;
                }

                return ResolveColumnType(resolvedTable, columnName);
            }
            else
            {
                // Unqualified — search all tables, return first match
                var columnName = identifiers[0].Value;

                if (_unqualifiedColumnTypeCache.TryGetValue(columnName, out var unqualifiedCached))
                {
                    return unqualifiedCached;
                }

                foreach (var table in _currentAliasMap.AllTables)
                {
                    var resolved = ResolveColumnType(table, columnName);
                    if (resolved is not null)
                    {
                        _unqualifiedColumnTypeCache[columnName] = resolved;
                        return resolved;
                    }
                }

                _unqualifiedColumnTypeCache[columnName] = null;
                return null;
            }
        }

        private bool TryResolveQualifiedTable(IList<Identifier> identifiers, out ResolvedTable? resolvedTable)
        {
            if (_currentAliasMap is null)
            {
                resolvedTable = null;
                return false;
            }

            return QualifierLookupKeyBuilder.TryResolve(_currentAliasMap, identifiers, out resolvedTable);
        }

        private SchemaTypeInfo? ResolveColumnType(ResolvedTable table, string columnName)
        {
            if (_tableColumnTypeCache.TryGetValue((table, columnName), out var cached))
            {
                return cached;
            }

            var resolved = schema.ResolveColumn(table, columnName)?.Column.Type;
            _tableColumnTypeCache[(table, columnName)] = resolved;
            return resolved;
        }

        private static readonly SchemaTypeInfo IntType = new("int", SchemaTypeCategory.ExactNumeric);
        private static readonly SchemaTypeInfo DecimalType = new("decimal", SchemaTypeCategory.ExactNumeric);
        private static readonly SchemaTypeInfo FloatType = new("float", SchemaTypeCategory.ApproximateNumeric);
        private static readonly SchemaTypeInfo MoneyType = new("money", SchemaTypeCategory.ExactNumeric);
        private static readonly SchemaTypeInfo NVarcharType = new("nvarchar", SchemaTypeCategory.UnicodeString);
        private static readonly SchemaTypeInfo VarcharType = new("varchar", SchemaTypeCategory.AnsiString);

        private static SchemaTypeInfo? InferLiteralType(ScalarExpression expression)
        {
            return expression switch
            {
                IntegerLiteral => IntType,
                NumericLiteral => DecimalType,
                RealLiteral => FloatType,
                MoneyLiteral => MoneyType,
                StringLiteral s when s.IsNational => NVarcharType,
                StringLiteral => VarcharType,
                _ => null
            };
        }

        private static string FormatColumnReference(ColumnReferenceExpression colRef)
        {
            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return "?";
            }

            return string.Join(".", identifiers.Select(i => i.Value));
        }

    }
}
