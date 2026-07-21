using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.ControlFlow;

/// <summary>Trust assigned to a value that may become part of dynamic SQL.</summary>
public enum SqlTrustKind
{
    Constant,
    UntrustedValue,
    EscapedStringLiteral,
    QuotedIdentifier,
    NumericValue,
    SqlFragment,
    Unknown
}

/// <summary>One bounded symbolic component of a dynamic SQL value.</summary>
public sealed record SqlSegment(SqlTrustKind Trust, string? ConstantText = null);

/// <summary>Trust and bounded symbolic content inferred for a SQL expression.</summary>
public sealed record SqlValueState(SqlTrustKind Trust, IReadOnlyList<SqlSegment>? Segments)
{
    public static SqlValueState FromConstant(string text) =>
        new(SqlTrustKind.Constant, [new SqlSegment(SqlTrustKind.Constant, text)]);

    public static SqlValueState FromTrust(SqlTrustKind trust) =>
        new(trust, [new SqlSegment(trust)]);

    /// <summary>Returns true when the symbolic value contains an unsafe dynamic SQL segment.</summary>
    public bool IsUnsafeSqlText()
    {
        if (Segments is null)
        {
            return Trust is not (SqlTrustKind.Constant or SqlTrustKind.NumericValue);
        }

        var insideStringLiteral = false;
        foreach (var segment in Segments)
        {
            switch (segment.Trust)
            {
                case SqlTrustKind.Constant:
                    UpdateStringLiteralContext(segment.ConstantText ?? string.Empty, ref insideStringLiteral);
                    break;
                case SqlTrustKind.NumericValue:
                    break;
                case SqlTrustKind.EscapedStringLiteral:
                    if (!insideStringLiteral)
                    {
                        return true;
                    }
                    break;
                case SqlTrustKind.QuotedIdentifier:
                    if (insideStringLiteral)
                    {
                        return true;
                    }
                    break;
                default:
                    return true;
            }
        }
        return false;
    }

    internal static void UpdateStringLiteralContext(string text, ref bool insideStringLiteral)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\'')
            {
                continue;
            }

            if (insideStringLiteral && index + 1 < text.Length && text[index + 1] == '\'')
            {
                index++;
                continue;
            }
            insideStringLiteral = !insideStringLiteral;
        }
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing control-flow analysis; tracked as coupling baseline debt.")]
internal sealed class SqlTaintAnalysis(ControlFlowScope scope, int maxSegments = 32)
    : ForwardDataFlowAnalysis<Dictionary<string, SqlValueState>>
{
    private static readonly SqlValueState s_unknown = SqlValueState.FromTrust(SqlTrustKind.Unknown);
    private static readonly SqlValueState s_untrusted = SqlValueState.FromTrust(SqlTrustKind.UntrustedValue);
    private static readonly SqlValueState s_numeric = SqlValueState.FromTrust(SqlTrustKind.NumericValue);
    private readonly int _maxSegments = maxSegments;

    protected override Dictionary<string, SqlValueState> InitialState()
    {
        var state = new Dictionary<string, SqlValueState>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in scope.Parameters)
        {
            if (parameter.VariableName is { } name)
            {
                state[name.Value] = s_untrusted;
            }
        }
        return state;
    }

    protected override Dictionary<string, SqlValueState> Transfer(
        Dictionary<string, SqlValueState> input,
        CfgNode node)
    {
        var output = new Dictionary<string, SqlValueState>(input, StringComparer.OrdinalIgnoreCase);
        switch (node.Statement)
        {
            case DeclareVariableStatement declarationStatement:
                foreach (var declaration in declarationStatement.Declarations)
                {
                    if (declaration.VariableName is { } name)
                    {
                        output[name.Value] = declaration.Value is null
                            ? s_unknown
                            : Evaluate(declaration.Value, output);
                    }
                }
                break;
            case SetVariableStatement set when set.Variable is not null:
                var assigned = Evaluate(set.Expression, output);
                output[set.Variable.Name] = set.AssignmentKind == AssignmentKind.Equals
                    ? assigned
                    : Concatenate(GetVariable(output, set.Variable.Name), assigned);
                break;
            case SelectStatement select:
                var visitor = new SelectAssignmentVisitor();
                select.Accept(visitor);
                foreach (var assignment in visitor.Assignments)
                {
                    var value = Evaluate(assignment.Expression, output);
                    output[assignment.Variable.Name] = assignment.AssignmentKind == AssignmentKind.Equals
                        ? value
                        : Concatenate(GetVariable(output, assignment.Variable.Name), value);
                }
                break;
        }
        if (node.Statement is not null)
        {
            foreach (var write in VariableAccessAnalysis.GetAccesses(node.Statement).Writes)
            {
                if (!IsDirectAssignment(node.Statement, write))
                {
                    output[write] = s_unknown;
                }
            }
        }
        return output;
    }

    private static bool IsDirectAssignment(TSqlStatement statement, string name) => statement switch
    {
        DeclareVariableStatement declaration => declaration.Declarations.Any(item =>
            string.Equals(item.VariableName?.Value, name, StringComparison.OrdinalIgnoreCase)),
        SetVariableStatement set => string.Equals(set.Variable?.Name, name, StringComparison.OrdinalIgnoreCase),
        SelectStatement select => HasSelectAssignment(select, name),
        _ => false
    };

    private static bool HasSelectAssignment(SelectStatement select, string name)
    {
        var visitor = new SelectAssignmentVisitor();
        select.Accept(visitor);
        return visitor.Assignments.Any(item =>
            string.Equals(item.Variable?.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    protected override Dictionary<string, SqlValueState> Merge(
        Dictionary<string, SqlValueState> left,
        Dictionary<string, SqlValueState> right)
    {
        var merged = new Dictionary<string, SqlValueState>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in left.Keys.Concat(right.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var leftValue = GetVariable(left, name);
            var rightValue = GetVariable(right, name);
            merged[name] = Join(leftValue, rightValue);
        }
        return merged;
    }

    protected override bool StateEquals(
        Dictionary<string, SqlValueState> left,
        Dictionary<string, SqlValueState> right) =>
        left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out var value) && ValueEquals(pair.Value, value));

    internal SqlValueState Evaluate(
        ScalarExpression? expression,
        IReadOnlyDictionary<string, SqlValueState> state)
    {
        return expression switch
        {
            null => s_unknown,
            StringLiteral literal => SqlValueState.FromConstant(literal.Value),
            NullLiteral => SqlValueState.FromConstant(string.Empty),
            IntegerLiteral or NumericLiteral or MoneyLiteral or RealLiteral => s_numeric,
            VariableReference variable => GetVariable(state, variable.Name),
            ColumnReferenceExpression => s_untrusted,
            ParenthesisExpression parenthesis => Evaluate(parenthesis.Expression, state),
            BinaryExpression binary when binary.BinaryExpressionType == BinaryExpressionType.Add =>
                Concatenate(Evaluate(binary.FirstExpression, state), Evaluate(binary.SecondExpression, state)),
            SimpleCaseExpression simpleCase => EvaluateCase(
                simpleCase.WhenClauses.Select(clause => clause.ThenExpression),
                simpleCase.ElseExpression,
                state),
            SearchedCaseExpression searchedCase => EvaluateCase(
                searchedCase.WhenClauses.Select(clause => clause.ThenExpression),
                searchedCase.ElseExpression,
                state),
            FunctionCall function => EvaluateFunction(function, state),
            CastCall cast when IsNumericType(cast.DataType) => s_numeric,
            ConvertCall convert when IsNumericType(convert.DataType) => s_numeric,
            UnaryExpression unary => Evaluate(unary.Expression, state),
            _ => s_unknown
        };
    }

    private SqlValueState EvaluateCase(
        IEnumerable<ScalarExpression> resultExpressions,
        ScalarExpression? elseExpression,
        IReadOnlyDictionary<string, SqlValueState> state)
    {
        var values = resultExpressions.Select(expression => Evaluate(expression, state)).ToList();
        values.Add(elseExpression is null
            ? SqlValueState.FromConstant(string.Empty)
            : Evaluate(elseExpression, state));

        return values.Aggregate(Join);
    }

    private SqlValueState EvaluateFunction(
        FunctionCall function,
        IReadOnlyDictionary<string, SqlValueState> state)
    {
        var name = function.FunctionName?.Value;
        if (string.Equals(name, "QUOTENAME", StringComparison.OrdinalIgnoreCase) &&
            function.Parameters.Count is 1 or 2)
        {
            return SqlValueState.FromTrust(SqlTrustKind.QuotedIdentifier);
        }

        if (string.Equals(name, "REPLACE", StringComparison.OrdinalIgnoreCase) &&
            function.Parameters.Count == 3 &&
            function.Parameters[1] is StringLiteral { Value: "'" } &&
            function.Parameters[2] is StringLiteral { Value: "''" })
        {
            return SqlValueState.FromTrust(SqlTrustKind.EscapedStringLiteral);
        }

        if (string.Equals(name, "CONCAT", StringComparison.OrdinalIgnoreCase))
        {
            return function.Parameters.Aggregate(
                SqlValueState.FromConstant(string.Empty),
                (current, parameter) => Concatenate(current, Evaluate(parameter, state)));
        }
        return s_unknown;
    }

    private SqlValueState Concatenate(SqlValueState left, SqlValueState right)
    {
        if (left.Segments is null || right.Segments is null ||
            left.Segments.Count + right.Segments.Count > _maxSegments)
        {
            return s_unknown;
        }

        var segments = left.Segments.Concat(right.Segments).ToArray();
        var trust = segments.All(segment => segment.Trust == SqlTrustKind.Constant)
            ? SqlTrustKind.Constant
            : segments.Any(segment => segment.Trust == SqlTrustKind.Unknown)
                ? SqlTrustKind.Unknown
                : SqlTrustKind.SqlFragment;
        return new SqlValueState(trust, segments);
    }

    private static SqlValueState GetVariable(
        IReadOnlyDictionary<string, SqlValueState> state,
        string name) => state.TryGetValue(name, out var value) ? value : s_unknown;

    private static bool ValueEquals(SqlValueState left, SqlValueState right) =>
        left.Trust == right.Trust &&
        ((left.Segments is null && right.Segments is null) ||
         (left.Segments is not null && right.Segments is not null &&
          left.Segments.SequenceEqual(right.Segments)));

    private static SqlValueState Join(SqlValueState left, SqlValueState right)
    {
        if (ValueEquals(left, right))
        {
            return left;
        }

        if (left.Trust == SqlTrustKind.Constant && right.Trust == SqlTrustKind.Constant)
        {
            var leftParity = GetQuoteParity(left);
            var rightParity = GetQuoteParity(right);
            return leftParity == rightParity
                ? SqlValueState.FromConstant(leftParity ? "'" : string.Empty)
                : s_unknown;
        }

        return left.Trust == right.Trust && left.Trust is not (SqlTrustKind.SqlFragment or SqlTrustKind.Unknown)
            ? SqlValueState.FromTrust(left.Trust)
            : s_unknown;
    }

    private static bool GetQuoteParity(SqlValueState value)
    {
        var inside = false;
        foreach (var segment in value.Segments ?? [])
        {
            if (segment.Trust == SqlTrustKind.Constant)
            {
                SqlValueState.UpdateStringLiteralContext(segment.ConstantText ?? string.Empty, ref inside);
            }
        }
        return inside;
    }

    private static bool IsNumericType(DataTypeReference? dataType) =>
        dataType is SqlDataTypeReference sqlType && sqlType.SqlDataTypeOption is
            SqlDataTypeOption.Bit or SqlDataTypeOption.TinyInt or SqlDataTypeOption.SmallInt or
            SqlDataTypeOption.Int or SqlDataTypeOption.BigInt or SqlDataTypeOption.Decimal or
            SqlDataTypeOption.Numeric or SqlDataTypeOption.Money or SqlDataTypeOption.SmallMoney or
            SqlDataTypeOption.Float or SqlDataTypeOption.Real;

    private sealed class SelectAssignmentVisitor : TSqlFragmentVisitor
    {
        internal List<SelectSetVariable> Assignments { get; } = [];

        public override void ExplicitVisit(SelectSetVariable node) => Assignments.Add(node);
    }
}
