using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Tests.TypeSystem;

public sealed class TypeCompatibilityTests
{
    [Fact]
    public void CheckComparison_SameType_ReturnsNone()
    {
        var left = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        Assert.Equal(ImplicitConversionResult.None, TypeCompatibility.CheckComparison(left, right));
    }

    [Fact]
    public void CheckComparison_SameTypeCaseInsensitive_ReturnsNone()
    {
        var left = new SchemaTypeInfo("INT", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        Assert.Equal(ImplicitConversionResult.None, TypeCompatibility.CheckComparison(left, right));
    }

    [Theory]
    [InlineData("varchar", "nvarchar")]
    [InlineData("nvarchar", "varchar")]
    public void CheckComparison_VarcharVsNvarchar_ConvertsLowerPrecedence(string leftType, string rightType)
    {
        var left = new SchemaTypeInfo(leftType, leftType.StartsWith("n", StringComparison.Ordinal) ? SchemaTypeCategory.UnicodeString : SchemaTypeCategory.AnsiString);
        var right = new SchemaTypeInfo(rightType, rightType.StartsWith("n", StringComparison.Ordinal) ? SchemaTypeCategory.UnicodeString : SchemaTypeCategory.AnsiString);

        var result = TypeCompatibility.CheckComparison(left, right);

        // varchar (AnsiString, precedence 40) vs nvarchar (UnicodeString, precedence 50)
        // Lower precedence side is converted
        if (leftType == "varchar")
        {
            Assert.Equal(ImplicitConversionResult.LeftConverted, result);
        }
        else
        {
            Assert.Equal(ImplicitConversionResult.RightConverted, result);
        }
    }

    [Fact]
    public void CheckComparison_IntVsBigint_ConvertsInt()
    {
        var left = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("bigint", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_BigintVsInt_ConvertsInt()
    {
        var left = new SchemaTypeInfo("bigint", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.RightConverted, result);
    }

    [Fact]
    public void CheckComparison_IntVsVarchar_ConvertsVarchar()
    {
        var left = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString);

        var result = TypeCompatibility.CheckComparison(left, right);

        // ExactNumeric (60) > AnsiString (40) → right (varchar) is converted
        Assert.Equal(ImplicitConversionResult.RightConverted, result);
    }

    [Fact]
    public void CheckComparison_VarcharVsInt_ConvertsVarchar()
    {
        var left = new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString);
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_DatetimeVsDatetime2_ConvertsDatetime()
    {
        var left = new SchemaTypeInfo("datetime", SchemaTypeCategory.DateTime);
        var right = new SchemaTypeInfo("datetime2", SchemaTypeCategory.DateTime);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_DateVsDatetime_ConvertsDate()
    {
        var left = new SchemaTypeInfo("date", SchemaTypeCategory.DateTime);
        var right = new SchemaTypeInfo("datetime", SchemaTypeCategory.DateTime);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_VarcharVsChar_SameCategory_ReturnsNone()
    {
        var left = new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString);
        var right = new SchemaTypeInfo("char", SchemaTypeCategory.AnsiString);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.None, result);
    }

    [Fact]
    public void CheckComparison_NvarcharVsNchar_SameCategory_ReturnsNone()
    {
        var left = new SchemaTypeInfo("nvarchar", SchemaTypeCategory.UnicodeString);
        var right = new SchemaTypeInfo("nchar", SchemaTypeCategory.UnicodeString);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.None, result);
    }

    [Fact]
    public void CheckComparison_DatetimeVsVarchar_ConvertsVarchar()
    {
        var left = new SchemaTypeInfo("datetime", SchemaTypeCategory.DateTime);
        var right = new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString);

        var result = TypeCompatibility.CheckComparison(left, right);

        // DateTime (80) > AnsiString (40) → right converted
        Assert.Equal(ImplicitConversionResult.RightConverted, result);
    }

    [Fact]
    public void CheckComparison_FloatVsDecimal_ConvertsDecimal()
    {
        var left = new SchemaTypeInfo("float", SchemaTypeCategory.ApproximateNumeric);
        var right = new SchemaTypeInfo("decimal", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        // ApproxNumeric (70) > ExactNumeric (60) → right converted
        Assert.Equal(ImplicitConversionResult.RightConverted, result);
    }

    [Fact]
    public void CheckComparison_SmallintVsInt_ConvertsSmallint()
    {
        var left = new SchemaTypeInfo("smallint", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_MoneyVsDecimal_ConvertsMoney()
    {
        var left = new SchemaTypeInfo("money", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("decimal", SchemaTypeCategory.ExactNumeric);

        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_UniqueidentifierVsVarchar_ConvertsUniqueidentifier()
    {
        var left = new SchemaTypeInfo("uniqueidentifier", SchemaTypeCategory.UniqueIdentifier);
        var right = new SchemaTypeInfo("varchar", SchemaTypeCategory.AnsiString);

        var result = TypeCompatibility.CheckComparison(left, right);

        // UniqueIdentifier (20) < AnsiString (40) → left converted
        Assert.Equal(ImplicitConversionResult.LeftConverted, result);
    }

    [Fact]
    public void CheckComparison_NullLeft_ThrowsArgumentNullException()
    {
        var right = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        Assert.Throws<ArgumentNullException>(() =>
            TypeCompatibility.CheckComparison(null!, right));
    }

    [Fact]
    public void CheckComparison_NullRight_ThrowsArgumentNullException()
    {
        var left = new SchemaTypeInfo("int", SchemaTypeCategory.ExactNumeric);

        Assert.Throws<ArgumentNullException>(() =>
            TypeCompatibility.CheckComparison(left, null!));
    }

    [Fact]
    public void CheckComparison_DecimalVsNumeric_SamePrecedence_ReturnsNone()
    {
        var left = new SchemaTypeInfo("decimal", SchemaTypeCategory.ExactNumeric);
        var right = new SchemaTypeInfo("numeric", SchemaTypeCategory.ExactNumeric);

        // decimal and numeric have same precedence (70)
        var result = TypeCompatibility.CheckComparison(left, right);

        Assert.Equal(ImplicitConversionResult.None, result);
    }

    [Fact]
    public void CheckComparison_DatetimeoffsetVsDatetime2_ConvertsDatetime2()
    {
        var left = new SchemaTypeInfo("datetimeoffset", SchemaTypeCategory.DateTime);
        var right = new SchemaTypeInfo("datetime2", SchemaTypeCategory.DateTime);

        var result = TypeCompatibility.CheckComparison(left, right);

        // datetimeoffset (60) > datetime2 (50) → right converted
        Assert.Equal(ImplicitConversionResult.RightConverted, result);
    }
}
