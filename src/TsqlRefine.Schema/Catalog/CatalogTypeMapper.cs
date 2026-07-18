using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Catalog;

internal static class CatalogTypeMapper
{
    internal static CatalogParameter FromParameter(ProcedureParameter parameter)
    {
        var (typeName, type) = FromDataType(parameter.DataType);
        return new CatalogParameter(
            parameter.VariableName.Value,
            typeName,
            type,
            parameter.Modifier == ParameterModifier.Output,
            parameter.Value is not null);
    }

    internal static (string DisplayName, SchemaTypeInfo Type) FromDataType(DataTypeReference dataType)
    {
        var generator = new Sql160ScriptGenerator();
        generator.GenerateScript(dataType, out var displayName);
        displayName = displayName.Trim();

        var baseName = dataType switch
        {
            SqlDataTypeReference sqlType => sqlType.SqlDataTypeOption.ToString(),
            UserDataTypeReference userType => userType.Name.BaseIdentifier?.Value ?? displayName,
            _ => dataType.Name?.BaseIdentifier?.Value ?? displayName
        };
        var normalizedName = baseName.ToLowerInvariant();
        var category = GetCategory(normalizedName);
        int? maxLength = null;
        int? precision = null;
        int? scale = null;

        if (dataType is ParameterizedDataTypeReference parameterized)
        {
            var values = parameterized.Parameters.Select(ParseInteger).ToArray();
            if (category is SchemaTypeCategory.AnsiString or SchemaTypeCategory.UnicodeString or SchemaTypeCategory.Binary)
            {
                maxLength = values.Length > 0 ? values[0] : null;
                if (maxLength is not null && category == SchemaTypeCategory.UnicodeString && maxLength != -1)
                {
                    maxLength *= 2;
                }
            }
            else if (normalizedName is "decimal" or "numeric")
            {
                precision = values.Length > 0 ? values[0] : null;
                scale = values.Length > 1 ? values[1] : null;
            }
        }

        return (displayName, new SchemaTypeInfo(normalizedName, category, maxLength, precision, scale));
    }

    private static int? ParseInteger(Literal literal)
    {
        if (string.Equals(literal.Value, "max", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }
        return int.TryParse(
            literal.Value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static SchemaTypeCategory GetCategory(string typeName) => typeName switch
    {
        "bit" or "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or
            "money" or "smallmoney" => SchemaTypeCategory.ExactNumeric,
        "float" or "real" => SchemaTypeCategory.ApproximateNumeric,
        "char" or "varchar" or "text" => SchemaTypeCategory.AnsiString,
        "nchar" or "nvarchar" or "ntext" or "sysname" => SchemaTypeCategory.UnicodeString,
        "date" or "time" or "datetime" or "datetime2" or "datetimeoffset" or "smalldatetime" =>
            SchemaTypeCategory.DateTime,
        "binary" or "varbinary" or "image" or "timestamp" or "rowversion" => SchemaTypeCategory.Binary,
        "uniqueidentifier" => SchemaTypeCategory.UniqueIdentifier,
        "xml" => SchemaTypeCategory.Xml,
        "geography" or "geometry" => SchemaTypeCategory.Spatial,
        _ => SchemaTypeCategory.Other
    };
}
