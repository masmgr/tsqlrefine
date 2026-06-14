using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.TypeSystem;

/// <summary>
/// Result of checking implicit conversion between two types in a comparison.
/// </summary>
public enum ImplicitConversionResult
{
    /// <summary>No implicit conversion needed (same type or compatible).</summary>
    None,

    /// <summary>The left operand is implicitly converted.</summary>
    LeftConverted,

    /// <summary>The right operand is implicitly converted.</summary>
    RightConverted,

    /// <summary>Both operands are implicitly converted.</summary>
    BothConverted
}

/// <summary>
/// Determines whether implicit type conversions occur when comparing two SQL Server types.
/// Based on SQL Server's type precedence rules.
/// </summary>
public static class TypeCompatibility
{
    /// <summary>
    /// Checks whether an implicit conversion occurs when comparing two types.
    /// </summary>
    /// <param name="left">The left operand type.</param>
    /// <param name="right">The right operand type.</param>
    /// <returns>Which side(s), if any, would be implicitly converted.</returns>
    public static ImplicitConversionResult CheckComparison(SchemaTypeInfo left, SchemaTypeInfo right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        // Same type name — no conversion
        if (string.Equals(left.TypeName, right.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            return ImplicitConversionResult.None;
        }

        // Same category — check within-category rules
        if (left.Category == right.Category)
        {
            return CheckSameCategory(left, right);
        }

        // Cross-category: use type precedence
        return CheckCrossCategory(left, right);
    }

    private static ImplicitConversionResult CheckSameCategory(SchemaTypeInfo left, SchemaTypeInfo right)
    {
        // Within the same numeric category — lower precedence type is converted
        if (left.Category is SchemaTypeCategory.ExactNumeric or SchemaTypeCategory.ApproximateNumeric)
        {
            var leftPrec = ImplicitConversionRules.GetNumericPrecedence(left.TypeName);
            var rightPrec = ImplicitConversionRules.GetNumericPrecedence(right.TypeName);

            if (leftPrec == rightPrec)
            {
                return ImplicitConversionResult.None;
            }

            return leftPrec < rightPrec
                ? ImplicitConversionResult.LeftConverted
                : ImplicitConversionResult.RightConverted;
        }

        // varchar vs char, nvarchar vs nchar — different type names but compatible
        if (left.Category is SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString)
        {
            return ImplicitConversionResult.None;
        }

        // DateTime subtypes — datetime2 has highest precedence
        if (left.Category is SchemaTypeCategory.DateTime)
        {
            var leftPrec = ImplicitConversionRules.GetDateTimePrecedence(left.TypeName);
            var rightPrec = ImplicitConversionRules.GetDateTimePrecedence(right.TypeName);

            if (leftPrec == rightPrec)
            {
                return ImplicitConversionResult.None;
            }

            return leftPrec < rightPrec
                ? ImplicitConversionResult.LeftConverted
                : ImplicitConversionResult.RightConverted;
        }

        return ImplicitConversionResult.None;
    }

    private static ImplicitConversionResult CheckCrossCategory(SchemaTypeInfo left, SchemaTypeInfo right)
    {
        var leftPrec = ImplicitConversionRules.GetCategoryPrecedence(left.Category);
        var rightPrec = ImplicitConversionRules.GetCategoryPrecedence(right.Category);

        if (leftPrec == rightPrec)
        {
            return ImplicitConversionResult.None;
        }

        // Lower precedence type gets converted to higher precedence type
        return leftPrec < rightPrec
            ? ImplicitConversionResult.LeftConverted
            : ImplicitConversionResult.RightConverted;
    }
}
