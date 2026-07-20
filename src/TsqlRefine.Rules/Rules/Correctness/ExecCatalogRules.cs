using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>Detects missing or extra arguments in statically resolved procedure calls.</summary>
public sealed class ExecParameterCountMismatchRule : ExecCatalogRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "exec-parameter-count-mismatch",
        "Detects EXEC calls with missing required or extra positional arguments.",
        "Correctness",
        RuleSeverity.Error,
        false);

    private protected override IEnumerable<ExecIssue> Validate(ExecCall call)
    {
        var supplied = call.Bindings
            .Where(binding => binding.Parameter is not null)
            .Select(binding => binding.Parameter!.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = call.Procedure.Parameters
            .Where(parameter => !parameter.HasDefault && !supplied.Contains(parameter.Name))
            .Select(parameter => parameter.Name)
            .ToArray();
        var extraCount = call.Bindings.Count(binding => binding.Parameter is null && binding.Argument.Variable is null);
        if (missing.Length > 0 || extraCount > 0)
        {
            var details = missing.Length > 0
                ? $"missing required parameter(s): {string.Join(", ", missing)}"
                : $"contains {extraCount} extra positional argument(s)";
            yield return new ExecIssue(call.Statement, $"EXEC '{call.DisplayName}' has an invalid argument count; {details}.");
        }
    }
}

/// <summary>Detects named EXEC arguments that do not exist in the procedure signature.</summary>
public sealed class ExecParameterNameMismatchRule : ExecCatalogRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "exec-parameter-name-mismatch",
        "Detects named EXEC arguments that are absent from the procedure signature.",
        "Correctness",
        RuleSeverity.Error,
        false);

    private protected override IEnumerable<ExecIssue> Validate(ExecCall call)
    {
        foreach (var binding in call.Bindings.Where(item => item.Argument.Variable is not null && item.Parameter is null))
        {
            yield return new ExecIssue(
                binding.Argument.Variable!,
                $"Parameter '{binding.Argument.Variable!.Name}' does not exist on procedure '{call.DisplayName}'.");
        }
    }
}

/// <summary>Detects calls that discard values from output parameters.</summary>
public sealed class ExecOutputNotCapturedRule : ExecCatalogRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "exec-output-not-captured",
        "Detects EXEC calls that omit OUTPUT when passing an output parameter.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ExecIssue> Validate(ExecCall call)
    {
        foreach (var binding in call.Bindings.Where(item => item.Parameter?.IsOutput == true && !item.Argument.IsOutput))
        {
            yield return new ExecIssue(
                binding.Argument,
                $"Output parameter '{binding.Parameter!.Name}' on procedure '{call.DisplayName}' is not captured with OUTPUT.");
        }
    }
}

/// <summary>Detects EXEC arguments whose known type may lose information during assignment.</summary>
public sealed class ExecParameterTypeMismatchRule : ExecCatalogRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "exec-parameter-type-mismatch",
        "Detects EXEC arguments with a known type that may lose information when assigned to the procedure parameter.",
        "Correctness",
        RuleSeverity.Warning,
        false);

    private protected override IEnumerable<ExecIssue> Validate(ExecCall call)
    {
        foreach (var binding in call.Bindings)
        {
            if (binding.Parameter is null ||
                ExecTypeInference.Infer(binding.Argument.ParameterValue, call.VariableTypes) is not { } argumentType ||
                !ExecTypeInference.IsPotentiallyLossy(argumentType, binding.Parameter.Type))
            {
                continue;
            }

            yield return new ExecIssue(
                binding.Argument.ParameterValue,
                $"Argument for parameter '{binding.Parameter.Name}' on procedure '{call.DisplayName}' may lose information "
                + $"when converted from '{argumentType.TypeName}' to '{binding.Parameter.Type.TypeName}'.");
        }
    }
}

/// <summary>Shared ScriptDOM-based implementation for object-catalog-backed EXEC rules.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing catalog-aware EXEC analysis; tracked as coupling baseline debt.")]
public abstract class ExecCatalogRuleBase : IRule
{
    public abstract RuleMetadata Metadata { get; }

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ast.Fragment is null || context.ObjectCatalog is not { HasData: true } catalog)
        {
            return [];
        }

        var visitor = new ExecuteCollector(catalog);
        context.Ast.Fragment.Accept(visitor);
        return visitor.Calls
            .SelectMany(Validate)
            .Select(issue => new Diagnostic(
                ScriptDomHelpers.GetRange(issue.Fragment),
                issue.Message,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private protected abstract IEnumerable<ExecIssue> Validate(ExecCall call);

    private sealed class ExecuteCollector(IObjectCatalogProvider catalog) : TSqlFragmentVisitor
    {
        private readonly Dictionary<string, SchemaTypeInfo> _variableTypes = new(StringComparer.OrdinalIgnoreCase);

        internal List<ExecCall> Calls { get; } = [];

        public override void ExplicitVisit(DeclareVariableStatement node)
        {
            foreach (var declaration in node.Declarations)
            {
                AddVariable(declaration.VariableName?.Value, declaration.DataType);
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ProcedureParameter node)
        {
            AddVariable(node.VariableName?.Value, node.DataType);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (TryCreateCall(node, catalog, _variableTypes, out var call))
            {
                Calls.Add(call);
            }
            base.ExplicitVisit(node);
        }

        private void AddVariable(string? name, DataTypeReference? dataType)
        {
            if (!string.IsNullOrWhiteSpace(name) && ExecTypeInference.FromDataType(dataType) is { } type)
            {
                _variableTypes[name] = type;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "Existing EXEC argument matching logic; tracked as complexity baseline debt.")]
    private static bool TryCreateCall(
        ExecuteStatement statement,
        IObjectCatalogProvider catalog,
        IReadOnlyDictionary<string, SchemaTypeInfo> variableTypes,
        out ExecCall call)
    {
        call = null!;
        if (statement.ExecuteSpecification?.ExecutableEntity is not ExecutableProcedureReference executable ||
            executable.ProcedureReference?.ProcedureReference?.Name is not { } name ||
            name.ServerIdentifier is not null ||
            name.BaseIdentifier?.Value is not { Length: > 0 } objectName)
        {
            return false;
        }

        var schema = name.SchemaIdentifier?.Value ?? catalog.Scope.DefaultSchema;
        var procedure = catalog.ResolveObject(
            name.DatabaseIdentifier?.Value,
            schema,
            objectName,
            CatalogObjectKindFilter.Procedure);
        if (procedure is null)
        {
            return false;
        }

        var bindings = new List<ExecBinding>(executable.Parameters.Count);
        var positionalIndex = 0;
        foreach (var argument in executable.Parameters)
        {
            CatalogParameterInfo? parameter;
            if (argument.Variable?.Name is { } parameterName)
            {
                parameter = procedure.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, parameterName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                parameter = positionalIndex < procedure.Parameters.Count
                    ? procedure.Parameters[positionalIndex]
                    : null;
                positionalIndex++;
            }
            bindings.Add(new ExecBinding(argument, parameter));
        }

        call = new ExecCall(
            statement,
            procedure,
            $"{procedure.Id.SchemaName}.{procedure.Id.Name}",
            bindings,
            variableTypes);
        return true;
    }
}

/// <summary>Validated static EXEC call.</summary>
internal sealed record ExecCall(
    ExecuteStatement Statement,
    CatalogObjectInfo Procedure,
    string DisplayName,
    IReadOnlyList<ExecBinding> Bindings,
    IReadOnlyDictionary<string, SchemaTypeInfo> VariableTypes);

/// <summary>Association between an EXEC argument and its resolved signature parameter.</summary>
internal sealed record ExecBinding(ExecuteParameter Argument, CatalogParameterInfo? Parameter);

/// <summary>Diagnostic candidate produced by an EXEC catalog rule.</summary>
internal sealed record ExecIssue(TSqlFragment Fragment, string Message);

internal static class ExecTypeInference
{
    internal static SchemaTypeInfo? Infer(
        ScalarExpression expression,
        IReadOnlyDictionary<string, SchemaTypeInfo> variableTypes) => expression switch
        {
            VariableReference variable when variableTypes.TryGetValue(variable.Name, out var type) => type,
            IntegerLiteral => new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric),
            NumericLiteral numeric => InferDecimal(numeric.Value),
            RealLiteral => new SchemaTypeInfo("float", SchemaTypeCategory.ApproximateNumeric),
            MoneyLiteral => new SchemaTypeInfo("money", SchemaTypeCategory.ExactNumeric),
            StringLiteral text when text.IsNational =>
                new SchemaTypeInfo("nvarchar", SchemaTypeCategory.UnicodeString, text.Value.Length * 2),
            StringLiteral text => new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString, text.Value.Length),
            CastCall cast => FromDataType(cast.DataType),
            ConvertCall convert => FromDataType(convert.DataType),
            ParenthesisExpression parenthesis => Infer(parenthesis.Expression, variableTypes),
            _ => null
        };

    internal static SchemaTypeInfo? FromDataType(DataTypeReference? dataType)
    {
        if (dataType is XmlDataTypeReference)
        {
            return new SchemaTypeInfo("xml", SchemaTypeCategory.Xml);
        }
        if (dataType is not SqlDataTypeReference sqlType)
        {
            return null;
        }
        var name = sqlType.SqlDataTypeOption.ToString().ToLowerInvariant();
        var category = sqlType.SqlDataTypeOption switch
        {
            SqlDataTypeOption.Bit or SqlDataTypeOption.TinyInt or SqlDataTypeOption.SmallInt or
                SqlDataTypeOption.Int or SqlDataTypeOption.BigInt or SqlDataTypeOption.Decimal or
                SqlDataTypeOption.Numeric or SqlDataTypeOption.Money or SqlDataTypeOption.SmallMoney =>
                SchemaTypeCategory.ExactNumeric,
            SqlDataTypeOption.Float or SqlDataTypeOption.Real => SchemaTypeCategory.ApproximateNumeric,
            SqlDataTypeOption.Char or SqlDataTypeOption.VarChar or SqlDataTypeOption.Text => SchemaTypeCategory.AnsiString,
            SqlDataTypeOption.NChar or SqlDataTypeOption.NVarChar or SqlDataTypeOption.NText => SchemaTypeCategory.UnicodeString,
            SqlDataTypeOption.Date or SqlDataTypeOption.Time or SqlDataTypeOption.DateTime or
                SqlDataTypeOption.DateTime2 or SqlDataTypeOption.DateTimeOffset or SqlDataTypeOption.SmallDateTime =>
                SchemaTypeCategory.DateTime,
            SqlDataTypeOption.Binary or SqlDataTypeOption.VarBinary or SqlDataTypeOption.Image => SchemaTypeCategory.Binary,
            SqlDataTypeOption.UniqueIdentifier => SchemaTypeCategory.UniqueIdentifier,
            _ => SchemaTypeCategory.Other
        };

        var values = sqlType.Parameters
            .Select(parameter => int.TryParse(parameter.Value, out var value) ? (int?)value : -1)
            .ToArray();
        var maxLength = category is SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString or SchemaTypeCategory.Binary
            ? values.FirstOrDefault()
            : null;
        if (maxLength is > 0 && category == SchemaTypeCategory.UnicodeString)
        {
            maxLength *= 2;
        }
        var precision = name is "decimal" or "numeric" ? values.ElementAtOrDefault(0) : null;
        var scale = name is "decimal" or "numeric" ? values.ElementAtOrDefault(1) : null;
        return new SchemaTypeInfo(name, category, maxLength, precision, scale);
    }

    internal static bool IsPotentiallyLossy(SchemaTypeInfo argument, SchemaTypeInfo parameter)
    {
        if (argument.Category is SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString &&
            parameter.Category is SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString)
        {
            if (argument.Category == SchemaTypeCategory.UnicodeString && parameter.Category == SchemaTypeCategory.AnsiString)
            {
                return true;
            }
            var argumentLength = GetStringCharacterCapacity(argument);
            var parameterLength = GetStringCharacterCapacity(parameter);
            return parameterLength is > -1 &&
                (argumentLength == -1 || argumentLength > parameterLength);
        }

        if (argument.Category == SchemaTypeCategory.ExactNumeric && parameter.Category == SchemaTypeCategory.ExactNumeric)
        {
            if (argument.Precision is not null && parameter.Precision is not null &&
                (argument.Precision > parameter.Precision || argument.Scale > parameter.Scale))
            {
                return true;
            }
            return TypeCompatibility.CheckComparison(argument, parameter) == ImplicitConversionResult.RightConverted;
        }

        if (argument.Category == parameter.Category && argument.Category == SchemaTypeCategory.DateTime)
        {
            return TypeCompatibility.CheckComparison(argument, parameter) == ImplicitConversionResult.RightConverted;
        }

        return false;
    }

    private static int? GetStringCharacterCapacity(SchemaTypeInfo type) =>
        type.Category == SchemaTypeCategory.UnicodeString && type.MaxLength is > -1
            ? type.MaxLength / 2
            : type.MaxLength;

    private static SchemaTypeInfo InferDecimal(string value)
    {
        var separator = value.IndexOf('.', StringComparison.Ordinal);
        var scale = separator < 0 ? 0 : value.Length - separator - 1;
        var precision = value.Count(char.IsDigit);
        return new SchemaTypeInfo("decimal", SchemaTypeCategory.ExactNumeric, Precision: precision, Scale: scale);
    }
}
