using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects assignments whose statically known string capacity exceeds the destination capacity.
/// </summary>
public sealed class StringAssignmentLengthMismatchRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "string-assignment-length-mismatch",
        Description: "Detects string assignments whose statically known maximum length exceeds the destination capacity.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new StringAssignmentLengthMismatchVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class StringAssignmentLengthMismatchVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        private readonly Dictionary<string, StringCapacity> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LocalTable> _localTables = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(TSqlBatch node)
        {
            _variables.Clear();
            _localTables.Clear();
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            AddProcedureParameters(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateOrAlterProcedureStatement node)
        {
            AddProcedureParameters(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterProcedureStatement node)
        {
            AddProcedureParameters(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                AddVariable(declaration.VariableName?.Value, declaration.DataType);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeclareTableVariableStatement node)
        {
            var name = node.Body?.VariableName?.Value;
            var columns = node.Body?.Definition?.ColumnDefinitions;
            AddLocalTable(name, columns);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateTableStatement node)
        {
            var name = node.SchemaObjectName?.BaseIdentifier?.Value;
            if (name?.StartsWith('#') == true)
            {
                AddLocalTable(name, node.Definition?.ColumnDefinitions);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SetVariableStatement node)
        {
            if (node.Variable?.Name is { } name &&
                _variables.TryGetValue(name, out var target))
            {
                CheckAssignment(target, node.Expression, ResolutionContext.Empty);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            var resolution = BuildResolutionContext(node.FromClause?.TableReferences);
            foreach (var selectElement in node.SelectElements)
            {
                if (selectElement is SelectSetVariable assignment &&
                    assignment.Variable?.Name is { } name &&
                    _variables.TryGetValue(name, out var target))
                {
                    CheckAssignment(target, assignment.Expression, resolution);
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UpdateStatement node)
        {
            var specification = node.UpdateSpecification;
            if (specification is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var resolution = BuildResolutionContext(specification.FromClause?.TableReferences, specification.Target);
            foreach (var clause in specification.SetClauses.OfType<AssignmentSetClause>())
            {
                if (clause.Column is null || clause.NewValue is null)
                {
                    continue;
                }

                var target = ResolveUpdateTargetCapacity(clause.Column, specification.Target, resolution);
                if (target is not null)
                {
                    CheckAssignment(target.Value, clause.NewValue, resolution);
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            var specification = node.InsertSpecification;
            if (specification?.Columns is not { Count: > 0 } columns)
            {
                base.ExplicitVisit(node);
                return;
            }

            var targets = columns
                .Select(column => ResolveInsertTargetCapacity(column, specification.Target))
                .ToArray();

            switch (specification.InsertSource)
            {
                case ValuesInsertSource values:
                    foreach (var row in values.RowValues)
                    {
                        CheckAlignedAssignments(
                            targets,
                            row.ColumnValues.Cast<ScalarExpression?>().ToArray(),
                            ResolutionContext.Empty);
                    }
                    break;

                case SelectInsertSource selectSource when
                    GetQuerySpecification(selectSource.Select) is { } query:
                    var resolution = BuildResolutionContext(query.FromClause?.TableReferences);
                    var expressions = query.SelectElements
                        .Select(element => element is SelectScalarExpression scalar ? scalar.Expression : null)
                        .ToArray();
                    CheckAlignedAssignments(targets, expressions, resolution);
                    break;
            }

            base.ExplicitVisit(node);
        }

        private void AddVariable(string? name, DataTypeReference? dataType)
        {
            var capacity = GetCapacity(dataType);
            if (!string.IsNullOrWhiteSpace(name) && capacity is not null)
            {
                _variables[name] = capacity.Value;
            }
        }

        private void AddProcedureParameters(ProcedureStatementBodyBase procedure)
        {
            foreach (var parameter in procedure.Parameters)
            {
                AddVariable(parameter.VariableName?.Value, parameter.DataType);
            }
        }

        private void AddLocalTable(string? name, IList<ColumnDefinition>? columns)
        {
            if (string.IsNullOrWhiteSpace(name) || columns is null)
            {
                return;
            }

            var local = new LocalTable(name);
            foreach (var column in columns)
            {
                var columnName = column.ColumnIdentifier?.Value;
                var capacity = GetCapacity(column.DataType);
                if (!string.IsNullOrWhiteSpace(columnName) && capacity is not null)
                {
                    local.Columns[columnName] = capacity.Value;
                }
            }

            _localTables[name] = local;
        }

        private void CheckAlignedAssignments(
            StringCapacity?[] targets,
            ScalarExpression?[] sources,
            ResolutionContext resolution)
        {
            var count = Math.Min(targets.Length, sources.Length);
            for (var i = 0; i < count; i++)
            {
                if (targets[i] is { } target && sources[i] is { } source)
                {
                    CheckAssignment(target, source, resolution);
                }
            }
        }

        private void CheckAssignment(
            StringCapacity target,
            ScalarExpression? expression,
            ResolutionContext resolution)
        {
            if (expression is null || target.IsMax)
            {
                return;
            }

            var source = InferCapacity(expression, resolution);
            if (source is null || (!source.Value.IsMax && source.Value.Characters <= target.Characters))
            {
                return;
            }

            var sourceDescription = source.Value.IsMax ? "MAX" : source.Value.Characters.ToString(CultureInfo.InvariantCulture);
            AddDiagnostic(
                fragment: expression,
                message: $"String expression can require {sourceDescription} characters, but the destination allows {target.Characters}. Widen the destination or explicitly bound the value.",
                code: "string-assignment-length-mismatch",
                category: "Correctness",
                fixable: false);
        }

        private StringCapacity? InferCapacity(ScalarExpression expression, ResolutionContext resolution)
        {
            return expression switch
            {
                StringLiteral literal => new StringCapacity(literal.IsNational, literal.Value?.Length ?? 0),
                VariableReference variable when variable.Name is { } name && _variables.TryGetValue(name, out var capacity) => capacity,
                ColumnReferenceExpression column => ResolveColumnCapacity(column, resolution),
                ParenthesisExpression parenthesis => InferCapacity(parenthesis.Expression, resolution),
                CastCall cast => GetCapacity(cast.DataType, defaultLength: 30),
                ConvertCall convert => GetCapacity(convert.DataType, defaultLength: 30),
                TryCastCall tryCast => GetCapacity(tryCast.DataType, defaultLength: 30),
                TryConvertCall tryConvert => GetCapacity(tryConvert.DataType, defaultLength: 30),
                BinaryExpression binary when binary.BinaryExpressionType == BinaryExpressionType.Add =>
                    Combine(InferCapacity(binary.FirstExpression, resolution), InferCapacity(binary.SecondExpression, resolution)),
                CoalesceExpression coalesce =>
                    Maximum(coalesce.Expressions.Select(item => InferCapacity(item, resolution))),
                FunctionCall function => InferFunctionCapacity(function, resolution),
                LeftFunctionCall left => InferSlicingCapacity(left.Parameters, resolution),
                RightFunctionCall right => InferSlicingCapacity(right.Parameters, resolution),
                _ => null
            };
        }

        private StringCapacity? InferFunctionCapacity(FunctionCall function, ResolutionContext resolution)
        {
            var name = function.FunctionName?.Value;
            if (name is null)
            {
                return null;
            }

            if (name.Equals("ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                if (function.Parameters.Count == 0)
                {
                    return null;
                }

                return IsNullLiteral(function.Parameters[0]) && function.Parameters.Count > 1
                    ? InferCapacity(function.Parameters[1], resolution)
                    : InferCapacity(function.Parameters[0], resolution);
            }

            if (name.Equals("COALESCE", StringComparison.OrdinalIgnoreCase))
            {
                return Maximum(function.Parameters.Select(parameter => InferCapacity(parameter, resolution)));
            }

            if (name.Equals("CONCAT", StringComparison.OrdinalIgnoreCase))
            {
                return Sum(function.Parameters.Select(parameter => InferCapacity(parameter, resolution)));
            }

            if (name.Equals("CONCAT_WS", StringComparison.OrdinalIgnoreCase) && function.Parameters.Count >= 2)
            {
                var separator = InferCapacity(function.Parameters[0], resolution);
                var values = Sum(function.Parameters.Skip(1).Select(parameter => InferCapacity(parameter, resolution)));
                if (separator is null || values is null)
                {
                    return null;
                }

                var separators = Multiply(separator.Value, Math.Max(0, function.Parameters.Count - 2));
                return Combine(values, separators);
            }

            if (name.Equals("SUBSTRING", StringComparison.OrdinalIgnoreCase))
            {
                return InferSubstringCapacity(function.Parameters, resolution);
            }

            if (name.Equals("STUFF", StringComparison.OrdinalIgnoreCase))
            {
                return InferStuffCapacity(function.Parameters, resolution);
            }

            return null;
        }

        private StringCapacity? InferSlicingCapacity(
            IList<ScalarExpression> parameters,
            ResolutionContext resolution)
        {
            if (parameters.Count == 0)
            {
                return null;
            }

            var source = InferCapacity(parameters[0], resolution);
            return parameters.Count > 1 && TryGetNonNegativeInteger(parameters[1], out var length)
                ? Limit(source, length)
                : source;
        }

        private StringCapacity? InferSubstringCapacity(
            IList<ScalarExpression> parameters,
            ResolutionContext resolution)
        {
            if (parameters.Count == 0)
            {
                return null;
            }

            var source = InferCapacity(parameters[0], resolution);
            return parameters.Count > 2 && TryGetNonNegativeInteger(parameters[2], out var length)
                ? Limit(source, length)
                : source;
        }

        private StringCapacity? InferStuffCapacity(
            IList<ScalarExpression> parameters,
            ResolutionContext resolution)
        {
            if (parameters.Count < 4)
            {
                return null;
            }

            var source = InferCapacity(parameters[0], resolution);
            var replacement = InferCapacity(parameters[3], resolution);
            if (source is null || replacement is null)
            {
                return null;
            }

            if (!TryGetNonNegativeInteger(parameters[1], out var start) ||
                !TryGetNonNegativeInteger(parameters[2], out var length))
            {
                return Combine(source, replacement);
            }

            if (start == 0 || (!source.Value.IsMax && start > source.Value.Characters))
            {
                return new StringCapacity(source.Value.Unicode || replacement.Value.Unicode, 0);
            }

            if (source.Value.IsMax || replacement.Value.IsMax)
            {
                return StringCapacity.Max(source.Value.Unicode || replacement.Value.Unicode);
            }

            var removableCharacters = Math.Max(0, source.Value.Characters - start + 1);
            var removedCharacters = Math.Min(length, removableCharacters);
            return new StringCapacity(
                source.Value.Unicode || replacement.Value.Unicode,
                checked(source.Value.Characters - removedCharacters + replacement.Value.Characters));
        }

        private StringCapacity? ResolveInsertTargetCapacity(
            ColumnReferenceExpression column,
            TableReference? target)
        {
            var columnName = GetColumnName(column);
            if (columnName is null)
            {
                return null;
            }

            if (TryGetTableKey(target, out var tableKey) &&
                _localTables.TryGetValue(tableKey, out var local) &&
                local.Columns.TryGetValue(columnName, out var localCapacity))
            {
                return localCapacity;
            }

            var resolved = ResolvePersistentTable(target);
            return resolved is null ? null : GetCapacity(schema?.ResolveColumn(resolved, columnName)?.Column.Type);
        }

        private StringCapacity? ResolveUpdateTargetCapacity(
            ColumnReferenceExpression column,
            TableReference? target,
            ResolutionContext resolution)
        {
            var directTarget = ResolveInsertTargetCapacity(column, target);
            if (directTarget is not null)
            {
                return directTarget;
            }

            return ResolveColumnCapacity(column, resolution);
        }

        private StringCapacity? ResolveColumnCapacity(
            ColumnReferenceExpression column,
            ResolutionContext resolution)
        {
            if (column.ColumnType == ColumnType.Wildcard ||
                column.MultiPartIdentifier?.Identifiers is not { Count: > 0 } identifiers)
            {
                return null;
            }

            var columnName = identifiers[^1].Value;
            if (identifiers.Count >= 2)
            {
                var qualifier = identifiers[^2].Value;
                if (resolution.LocalAliases.TryGetValue(qualifier, out var local))
                {
                    return local.Columns.TryGetValue(columnName, out var localCapacity) ? localCapacity : null;
                }

                if (schema is not null && resolution.SchemaAliases is not null &&
                    QualifierLookupKeyBuilder.TryResolve(resolution.SchemaAliases, identifiers, out var table) &&
                    table is not null)
                {
                    return GetCapacity(schema.ResolveColumn(table, columnName)?.Column.Type);
                }

                return null;
            }

            StringCapacity? match = null;
            var matchCount = 0;
            foreach (var local in resolution.LocalAliases.Values.Distinct())
            {
                if (local.Columns.TryGetValue(columnName, out var localCapacity))
                {
                    match = localCapacity;
                    matchCount++;
                }
            }

            if (schema is not null && resolution.SchemaAliases is not null)
            {
                foreach (var table in resolution.SchemaAliases.AllTables.Distinct())
                {
                    var resolved = schema.ResolveColumn(table, columnName);
                    if (resolved is not null)
                    {
                        match = GetCapacity(resolved.Column.Type);
                        if (match is not null)
                        {
                            matchCount++;
                        }
                    }
                }
            }

            return matchCount == 1 ? match : null;
        }

        private ResolutionContext BuildResolutionContext(
            IList<TableReference>? tableReferences,
            TableReference? additionalReference = null)
        {
            var references = tableReferences?.ToList() ?? [];
            if (references.Count == 0 && additionalReference is not null)
            {
                references.Add(additionalReference);
            }

            if (references.Count == 0)
            {
                return ResolutionContext.Empty;
            }

            var localAliases = new Dictionary<string, LocalTable>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in references)
            {
                CollectLocalAliases(reference, localAliases);
            }

            var schemaAliases = schema is null ? null : AliasMapBuilder.Build(references, schema);
            return new ResolutionContext(localAliases, schemaAliases);
        }

        private void CollectLocalAliases(TableReference reference, Dictionary<string, LocalTable> aliases)
        {
            switch (reference)
            {
                case JoinTableReference join:
                    CollectLocalAliases(join.FirstTableReference, aliases);
                    CollectLocalAliases(join.SecondTableReference, aliases);
                    return;
                case JoinParenthesisTableReference parenthesis when parenthesis.Join is not null:
                    CollectLocalAliases(parenthesis.Join, aliases);
                    return;
                case NamedTableReference named:
                    var name = named.SchemaObject?.BaseIdentifier?.Value;
                    if (name is not null && _localTables.TryGetValue(name, out var namedLocal))
                    {
                        aliases[named.Alias?.Value ?? name] = namedLocal;
                    }
                    return;
                case VariableTableReference variable:
                    var variableName = variable.Variable?.Name;
                    if (variableName is not null && _localTables.TryGetValue(variableName, out var variableLocal))
                    {
                        aliases[variable.Alias?.Value ?? variableName] = variableLocal;
                    }
                    return;
            }
        }

        private ResolvedTable? ResolvePersistentTable(TableReference? reference)
        {
            if (schema is null || reference is not NamedTableReference named)
            {
                return null;
            }

            var objectName = named.SchemaObject;
            var tableName = objectName?.BaseIdentifier?.Value;
            if (tableName is null || AliasMapBuilder.IsTemporaryOrVariable(tableName))
            {
                return null;
            }

            return schema.ResolveTable(
                objectName?.DatabaseIdentifier?.Value,
                objectName?.SchemaIdentifier?.Value,
                tableName);
        }

        private static bool TryGetTableKey(TableReference? reference, out string key)
        {
            switch (reference)
            {
                case NamedTableReference named when named.SchemaObject?.BaseIdentifier?.Value is { } name:
                    key = name;
                    return true;
                case VariableTableReference variable when variable.Variable?.Name is { } variableName:
                    key = variableName;
                    return true;
                default:
                    key = string.Empty;
                    return false;
            }
        }

        private static string? GetColumnName(ColumnReferenceExpression column) =>
            column.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;

        private static QuerySpecification? GetQuerySpecification(QueryExpression? expression) =>
            expression switch
            {
                QuerySpecification query => query,
                QueryParenthesisExpression parenthesis => GetQuerySpecification(parenthesis.QueryExpression),
                _ => null
            };

        private static StringCapacity? GetCapacity(DataTypeReference? dataType, int defaultLength = 1)
        {
            if (dataType is not SqlDataTypeReference sqlType)
            {
                return null;
            }

            var unicode = sqlType.SqlDataTypeOption is SqlDataTypeOption.NChar or SqlDataTypeOption.NVarChar or SqlDataTypeOption.NText;
            var ansi = sqlType.SqlDataTypeOption is SqlDataTypeOption.Char or SqlDataTypeOption.VarChar or SqlDataTypeOption.Text;
            if (!unicode && !ansi)
            {
                return null;
            }

            if (sqlType.SqlDataTypeOption is SqlDataTypeOption.Text or SqlDataTypeOption.NText ||
                sqlType.Parameters.Any(parameter => parameter.LiteralType == LiteralType.Max))
            {
                return StringCapacity.Max(unicode);
            }

            if (sqlType.Parameters.Count == 0)
            {
                return new StringCapacity(unicode, defaultLength);
            }

            return int.TryParse(sqlType.Parameters[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var characters)
                ? new StringCapacity(unicode, characters)
                : null;
        }

        private static StringCapacity? GetCapacity(SchemaTypeInfo? type)
        {
            if (type?.Category is not (SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString) ||
                type.MaxLength is null)
            {
                return null;
            }

            var unicode = type.Category == SchemaTypeCategory.UnicodeString;
            if (type.MaxLength == -1)
            {
                return StringCapacity.Max(unicode);
            }

            return new StringCapacity(unicode, unicode ? type.MaxLength.Value / 2 : type.MaxLength.Value);
        }

        private static StringCapacity? Combine(StringCapacity? left, StringCapacity? right)
        {
            if (left is null || right is null)
            {
                return null;
            }

            if (left.Value.IsMax || right.Value.IsMax)
            {
                return StringCapacity.Max(left.Value.Unicode || right.Value.Unicode);
            }

            return new StringCapacity(
                left.Value.Unicode || right.Value.Unicode,
                checked(left.Value.Characters + right.Value.Characters));
        }

        private static StringCapacity? Limit(StringCapacity? capacity, int maximumCharacters)
        {
            if (capacity is null)
            {
                return null;
            }

            return capacity.Value with
            {
                Characters = capacity.Value.IsMax
                    ? maximumCharacters
                    : Math.Min(capacity.Value.Characters, maximumCharacters)
            };
        }

        private static bool TryGetNonNegativeInteger(ScalarExpression expression, out int value)
        {
            value = 0;
            while (expression is ParenthesisExpression parenthesis)
            {
                expression = parenthesis.Expression;
            }

            return expression is IntegerLiteral literal &&
                int.TryParse(literal.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                value >= 0;
        }

        private static bool IsNullLiteral(ScalarExpression expression)
        {
            while (expression is ParenthesisExpression parenthesis)
            {
                expression = parenthesis.Expression;
            }

            return expression is NullLiteral;
        }

        private static StringCapacity? Sum(IEnumerable<StringCapacity?> capacities)
        {
            StringCapacity? result = new StringCapacity(false, 0);
            foreach (var capacity in capacities)
            {
                result = Combine(result, capacity);
                if (result is null)
                {
                    return null;
                }
            }

            return result;
        }

        private static StringCapacity? Maximum(IEnumerable<StringCapacity?> capacities)
        {
            StringCapacity? result = null;
            foreach (var capacity in capacities)
            {
                if (capacity is null)
                {
                    return null;
                }

                if (result is null || capacity.Value.IsMax ||
                    (!result.Value.IsMax && capacity.Value.Characters > result.Value.Characters))
                {
                    result = capacity;
                }
                else if (capacity.Value.Unicode && !result.Value.Unicode)
                {
                    result = result.Value with { Unicode = true };
                }
            }

            return result;
        }

        private static StringCapacity Multiply(StringCapacity capacity, int multiplier) =>
            capacity.IsMax
                ? capacity
                : capacity with { Characters = checked(capacity.Characters * multiplier) };

        private readonly record struct StringCapacity(bool Unicode, int Characters)
        {
            public bool IsMax => Characters < 0;

            public static StringCapacity Max(bool unicode) => new(unicode, -1);
        }

        private sealed class LocalTable(string name)
        {
            public string Name { get; } = name;

            public Dictionary<string, StringCapacity> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed record ResolutionContext(
            IReadOnlyDictionary<string, LocalTable> LocalAliases,
            AliasMap? SchemaAliases)
        {
            public static ResolutionContext Empty { get; } = new(
                new Dictionary<string, LocalTable>(StringComparer.OrdinalIgnoreCase),
                null);
        }
    }
}
