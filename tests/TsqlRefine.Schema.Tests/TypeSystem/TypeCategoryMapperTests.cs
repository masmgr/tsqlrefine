using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Tests.TypeSystem;

public class TypeCategoryMapperTests
{
    [Theory]
    [InlineData("int", TypeCategory.ExactNumeric)]
    [InlineData("bigint", TypeCategory.ExactNumeric)]
    [InlineData("smallint", TypeCategory.ExactNumeric)]
    [InlineData("tinyint", TypeCategory.ExactNumeric)]
    [InlineData("decimal", TypeCategory.ExactNumeric)]
    [InlineData("numeric", TypeCategory.ExactNumeric)]
    [InlineData("money", TypeCategory.ExactNumeric)]
    [InlineData("smallmoney", TypeCategory.ExactNumeric)]
    [InlineData("bit", TypeCategory.ExactNumeric)]
    [InlineData("float", TypeCategory.ApproximateNumeric)]
    [InlineData("real", TypeCategory.ApproximateNumeric)]
    [InlineData("char", TypeCategory.AnsiString)]
    [InlineData("varchar", TypeCategory.AnsiString)]
    [InlineData("text", TypeCategory.AnsiString)]
    [InlineData("nchar", TypeCategory.UnicodeString)]
    [InlineData("nvarchar", TypeCategory.UnicodeString)]
    [InlineData("ntext", TypeCategory.UnicodeString)]
    [InlineData("date", TypeCategory.DateTime)]
    [InlineData("time", TypeCategory.DateTime)]
    [InlineData("datetime", TypeCategory.DateTime)]
    [InlineData("datetime2", TypeCategory.DateTime)]
    [InlineData("datetimeoffset", TypeCategory.DateTime)]
    [InlineData("smalldatetime", TypeCategory.DateTime)]
    [InlineData("binary", TypeCategory.Binary)]
    [InlineData("varbinary", TypeCategory.Binary)]
    [InlineData("image", TypeCategory.Binary)]
    [InlineData("uniqueidentifier", TypeCategory.UniqueIdentifier)]
    [InlineData("xml", TypeCategory.Xml)]
    [InlineData("geography", TypeCategory.Spatial)]
    [InlineData("geometry", TypeCategory.Spatial)]
    [InlineData("sql_variant", TypeCategory.Other)]
    [InlineData("hierarchyid", TypeCategory.Other)]
    public void FromTypeName_KnownTypes_ReturnsCorrectCategory(string typeName, TypeCategory expected)
    {
        Assert.Equal(expected, TypeCategoryMapper.FromTypeName(typeName));
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("Int")]
    [InlineData("NVARCHAR")]
    [InlineData("DateTime2")]
    public void FromTypeName_CaseInsensitive(string typeName)
    {
        var result = TypeCategoryMapper.FromTypeName(typeName);
        Assert.NotEqual(TypeCategory.Other, result);
    }

    [Theory]
    [InlineData("unknown_type")]
    [InlineData("myCustomType")]
    public void FromTypeName_UnknownType_ReturnsOther(string typeName)
    {
        Assert.Equal(TypeCategory.Other, TypeCategoryMapper.FromTypeName(typeName));
    }

    [Fact]
    public void FromTypeName_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TypeCategoryMapper.FromTypeName(null!));
    }
}
