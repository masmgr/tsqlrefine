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

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);

                node.WhereClause?.Accept(this);
                node.HavingClause?.Accept(this);
                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
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
            if (expression is ColumnReferenceExpression colRef)
            {
                return ResolveColumnType(colRef);
            }

            return InferLiteralType(expression);
        }

        private SchemaTypeInfo? ResolveColumnType(ColumnReferenceExpression colRef)
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
                var tableAlias = identifiers[identifiers.Count - 2].Value;
                var columnName = identifiers[identifiers.Count - 1].Value;

                if (!_currentAliasMap.TryResolve(tableAlias, out var resolvedTable) || resolvedTable is null)
                {
                    return null;
                }

                var resolved = schema.ResolveColumn(resolvedTable, columnName);
                return resolved?.Column.Type;
            }
            else
            {
                // Unqualified — search all tables, return first match
                var columnName = identifiers[0].Value;
                foreach (var table in _currentAliasMap.AllTables)
                {
                    var resolved = schema.ResolveColumn(table, columnName);
                    if (resolved is not null)
                    {
                        return resolved.Column.Type;
                    }
                }

                return null;
            }
        }

        private static SchemaTypeInfo? InferLiteralType(ScalarExpression expression)
        {
            return expression switch
            {
                IntegerLiteral => new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric),
                NumericLiteral => new SchemaTypeInfo("decimal", SchemaTypeCategory.ExactNumeric),
                RealLiteral => new SchemaTypeInfo("float", SchemaTypeCategory.ApproximateNumeric),
                MoneyLiteral => new SchemaTypeInfo("money", SchemaTypeCategory.ExactNumeric),
                StringLiteral s when s.IsNational => new SchemaTypeInfo("nvarchar", SchemaTypeCategory.UnicodeString),
                StringLiteral => new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString),
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
