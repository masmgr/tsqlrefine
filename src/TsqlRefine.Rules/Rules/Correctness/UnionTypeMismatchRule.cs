using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects UNION/UNION ALL where corresponding columns have obviously different types, which may cause implicit conversion or data truncation.
/// When schema information is available, resolves column reference types for more comprehensive detection.
/// </summary>
public sealed class UnionTypeMismatchRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "union-type-mismatch",
        Description: "Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnionTypeMismatchVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnionTypeMismatchVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BinaryQueryExpression node)
        {
            if (node.BinaryQueryExpressionType is BinaryQueryExpressionType.Union)
            {
                CheckUnionTypeMismatch(node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckUnionTypeMismatch(BinaryQueryExpression node)
        {
            var leftColumns = GetSelectElements(node.FirstQueryExpression);
            var rightColumns = GetSelectElements(node.SecondQueryExpression);

            if (leftColumns is null || rightColumns is null)
            {
                return;
            }

            var count = Math.Min(leftColumns.Count, rightColumns.Count);

            // Build alias maps lazily (only when needed for column refs)
            AliasMap? leftAliasMap = null;
            AliasMap? rightAliasMap = null;

            for (var i = 0; i < count; i++)
            {
                var leftType = GetLiteralCategory(leftColumns[i]);
                var rightType = GetLiteralCategory(rightColumns[i]);

                // Try schema resolution when literal detection returns null
                if (schema is not null)
                {
                    if (leftType is null)
                    {
                        leftAliasMap ??= BuildAliasMapForLeg(node.FirstQueryExpression);
                        leftType = ResolveColumnCategory(leftColumns[i], leftAliasMap);
                    }

                    if (rightType is null)
                    {
                        rightAliasMap ??= BuildAliasMapForLeg(node.SecondQueryExpression);
                        rightType = ResolveColumnCategory(rightColumns[i], rightAliasMap);
                    }
                }

                if (leftType is not null && rightType is not null && leftType != rightType)
                {
                    AddDiagnostic(
                        fragment: rightColumns[i],
                        message: $"UNION column {i + 1} has mismatched types: left side is {leftType}, right side is {rightType}. This causes implicit conversion and may lead to data truncation or conversion errors.",
                        code: "union-type-mismatch",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }

        private AliasMap? BuildAliasMapForLeg(QueryExpression? queryExpression)
        {
            var spec = GetQuerySpecification(queryExpression);
            if (spec?.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                return AliasMapBuilder.Build(tableRefs, schema!);
            }

            return null;
        }

        private static QuerySpecification? GetQuerySpecification(QueryExpression? queryExpression)
        {
            return queryExpression switch
            {
                QuerySpecification spec => spec,
                QueryParenthesisExpression paren => GetQuerySpecification(paren.QueryExpression),
                BinaryQueryExpression bqe => GetQuerySpecification(bqe.SecondQueryExpression),
                _ => null
            };
        }

        private string? ResolveColumnCategory(SelectElement element, AliasMap? aliasMap)
        {
            if (aliasMap is null)
            {
                return null;
            }

            var expression = element is SelectScalarExpression scalar ? scalar.Expression : null;

            // Unwrap parenthesized expressions
            while (expression is ParenthesisExpression paren)
            {
                expression = paren.Expression;
            }

            if (expression is not ColumnReferenceExpression colRef
                || colRef.ColumnType == ColumnType.Wildcard)
            {
                return null;
            }

            var resolved = ResolveColumn(colRef, aliasMap);
            return resolved is null ? null : MapSchemaTypeCategory(resolved.Column.Type.Category);
        }

        private ResolvedColumn? ResolveColumn(ColumnReferenceExpression colRef, AliasMap aliasMap)
        {
            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return null;
            }

            if (identifiers.Count >= 2)
            {
                var columnName = identifiers[^1].Value;

                if (!TryResolveQualifiedTable(identifiers, aliasMap, out var resolvedTable)
                    || resolvedTable is null)
                {
                    return null;
                }

                return schema!.ResolveColumn(resolvedTable, columnName);
            }

            // Unqualified column — search all tables in the alias map
            var unqualifiedName = identifiers[0].Value;
            foreach (var table in aliasMap.AllTables)
            {
                var resolved = schema!.ResolveColumn(table, unqualifiedName);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static bool TryResolveQualifiedTable(
            IList<Identifier> identifiers,
            AliasMap aliasMap,
            out ResolvedTable? resolvedTable)
        {
            foreach (var key in BuildQualifierLookupKeys(identifiers))
            {
                if (aliasMap.TryResolve(key, out resolvedTable))
                {
                    return true;
                }
            }

            resolvedTable = null;
            return false;
        }

        private static IEnumerable<string> BuildQualifierLookupKeys(IList<Identifier> identifiers)
        {
            var qualifierCount = identifiers.Count - 1;
            if (qualifierCount <= 0)
            {
                yield break;
            }

            var parts = new string[qualifierCount];
            for (var i = 0; i < qualifierCount; i++)
            {
                parts[i] = identifiers[i].Value;
            }

            if (parts.Length == 1)
            {
                yield return parts[0];
                yield break;
            }

            yield return string.Join(".", parts);

            if (parts.Length >= 2)
            {
                yield return $"{parts[^2]}.{parts[^1]}";
            }

            yield return parts[^1];
        }

        private static string? MapSchemaTypeCategory(SchemaTypeCategory category)
        {
            return category switch
            {
                SchemaTypeCategory.ExactNumeric => "numeric",
                SchemaTypeCategory.ApproximateNumeric => "numeric",
                SchemaTypeCategory.AnsiString => "string",
                SchemaTypeCategory.UnicodeString => "string",
                SchemaTypeCategory.DateTime => "datetime",
                SchemaTypeCategory.Binary => "binary",
                SchemaTypeCategory.UniqueIdentifier => "uniqueidentifier",
                _ => null
            };
        }

        private static IList<SelectElement>? GetSelectElements(QueryExpression? queryExpression)
        {
            if (queryExpression is QuerySpecification querySpec)
            {
                return querySpec.SelectElements;
            }

            if (queryExpression is QueryParenthesisExpression paren)
            {
                return GetSelectElements(paren.QueryExpression);
            }

            // For nested UNION, get the right-most query's columns
            if (queryExpression is BinaryQueryExpression bqe)
            {
                return GetSelectElements(bqe.SecondQueryExpression);
            }

            return null;
        }

        private static string? GetLiteralCategory(SelectElement element)
        {
            var expression = element is SelectScalarExpression scalar ? scalar.Expression : null;

            // Unwrap parenthesized expressions
            while (expression is ParenthesisExpression paren)
            {
                expression = paren.Expression;
            }

            // Unwrap CAST/CONVERT — the target type is what matters, not the source
            if (expression is CastCall castCall)
            {
                return GetDataTypeName(castCall.DataType);
            }

            if (expression is ConvertCall convertCall)
            {
                return GetDataTypeName(convertCall.DataType);
            }

            if (expression is TryCastCall tryCastCall)
            {
                return GetDataTypeName(tryCastCall.DataType);
            }

            if (expression is TryConvertCall tryConvertCall)
            {
                return GetDataTypeName(tryConvertCall.DataType);
            }

            return expression switch
            {
                IntegerLiteral => "numeric",
                NumericLiteral => "numeric",
                RealLiteral => "numeric",
                MoneyLiteral => "numeric",
                StringLiteral => "string",
                NullLiteral => null,   // NULL is compatible with any type
                _ => null              // Can't determine type from column refs, expressions, etc.
            };
        }

        private static string? GetDataTypeName(DataTypeReference? dataType)
        {
            if (dataType is SqlDataTypeReference sqlType)
            {
                return sqlType.SqlDataTypeOption switch
                {
                    SqlDataTypeOption.Int or
                    SqlDataTypeOption.BigInt or
                    SqlDataTypeOption.SmallInt or
                    SqlDataTypeOption.TinyInt or
                    SqlDataTypeOption.Decimal or
                    SqlDataTypeOption.Numeric or
                    SqlDataTypeOption.Float or
                    SqlDataTypeOption.Real or
                    SqlDataTypeOption.Money or
                    SqlDataTypeOption.SmallMoney or
                    SqlDataTypeOption.Bit => "numeric",

                    SqlDataTypeOption.Char or
                    SqlDataTypeOption.VarChar or
                    SqlDataTypeOption.NChar or
                    SqlDataTypeOption.NVarChar or
                    SqlDataTypeOption.Text or
                    SqlDataTypeOption.NText => "string",

                    SqlDataTypeOption.Date or
                    SqlDataTypeOption.DateTime or
                    SqlDataTypeOption.DateTime2 or
                    SqlDataTypeOption.SmallDateTime or
                    SqlDataTypeOption.DateTimeOffset or
                    SqlDataTypeOption.Time => "datetime",

                    SqlDataTypeOption.Binary or
                    SqlDataTypeOption.VarBinary or
                    SqlDataTypeOption.Image => "binary",

                    SqlDataTypeOption.UniqueIdentifier => "uniqueidentifier",

                    _ => null
                };
            }

            return null;
        }
    }
}
