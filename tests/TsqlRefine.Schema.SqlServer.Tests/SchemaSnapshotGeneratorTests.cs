using System.Collections.Frozen;
using Microsoft.Data.SqlClient;

namespace TsqlRefine.Schema.SqlServer.Tests;

public sealed class SchemaSnapshotGeneratorTests
{
    [Fact]
    public void BuildExcludeSet_NoCustomSchemas_ContainsSystemSchemasCaseInsensitively()
    {
        var result = SchemaSnapshotGenerator.BuildExcludeSet(new SchemaSnapshotOptions());

        Assert.Equal(2, result.Count);
        Assert.Contains("SYS", result.AsEnumerable());
        Assert.Contains("information_schema", result.AsEnumerable());
    }

    [Fact]
    public void BuildExcludeSet_CustomSchemas_MergesCustomAndSystemSchemas()
    {
        var options = new SchemaSnapshotOptions(ExcludeSchemas: ["audit", "Sys"]);

        var result = SchemaSnapshotGenerator.BuildExcludeSet(options);

        Assert.Equal(3, result.Count);
        Assert.Contains("audit", result.AsEnumerable());
        Assert.Contains("sys", result.AsEnumerable());
        Assert.Contains("INFORMATION_SCHEMA", result.AsEnumerable());
    }

    [Theory]
    [InlineData("dbo", true)]
    [InlineData("sales", false)]
    [InlineData("sys", false)]
    [InlineData("SYS", false)]
    public void ShouldInclude_IncludeAndExcludeFilters_ReturnsExpected(string schemaName, bool expected)
    {
        var include = new[] { "dbo", "sys" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var exclude = new[] { "sys" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        var result = SchemaSnapshotGenerator.ShouldInclude(schemaName, include, exclude);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CreateCatalogCommand_FiltersSchemasWithParameters()
    {
        var include = new[] { "sales", "dbo" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var exclude = new[] { "sys" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        using var connection = new SqlConnection();

        using var command = SchemaSnapshotGenerator.CreateCatalogCommand(
            CatalogQueries.TablesAndViews,
            connection,
            include,
            exclude);

        Assert.DoesNotContain(CatalogQueries.SchemaFilterMarker, command.CommandText, StringComparison.Ordinal);
        Assert.Contains("AND s.name IN (@includeSchema0, @includeSchema1)", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("AND s.name NOT IN (@excludeSchema0)", command.CommandText, StringComparison.Ordinal);
        Assert.Equal("dbo", command.Parameters["@includeSchema0"].Value);
        Assert.Equal("sales", command.Parameters["@includeSchema1"].Value);
        Assert.Equal("sys", command.Parameters["@excludeSchema0"].Value);
    }

    [Fact]
    public void BuildTableSchemas_RelatedEntries_GroupsAndPreservesMetadata()
    {
        var tables = new List<SchemaSnapshotGenerator.TableEntry>
        {
            new("dbo", "Users", IsView: false),
            new("reporting", "ActiveUsers", IsView: true)
        };
        var columns = new List<SchemaSnapshotGenerator.ColumnEntry>
        {
            new("DBO", "users", "Id", "int", 4, 10, 0, false, true, false, null, null),
            new("dbo", "Users", "Email", "nvarchar", 200, 0, 0, false, false, false, "('')", "Latin1_General_CI_AS")
        };
        var primaryKeys = new List<SchemaSnapshotGenerator.PkEntry>
        {
            new("dbo", "Users", IsClustered: true, "Id")
        };
        var uniqueConstraints = new List<SchemaSnapshotGenerator.UqEntry>
        {
            new("dbo", "Users", "UQ_Users_Email", "Email")
        };
        var foreignKeys = new List<SchemaSnapshotGenerator.FkEntry>
        {
            new("dbo", "Users", "FK_Users_Tenant", "TenantId", "dbo", "Tenants", "Id")
        };
        var indexes = new List<SchemaSnapshotGenerator.IdxEntry>
        {
            new("dbo", "Users", "IX_Users_Email", IsUnique: true, IsClustered: false, "Email")
        };

        var result = SchemaSnapshotGenerator.BuildTableSchemas(
            tables,
            columns,
            primaryKeys,
            uniqueConstraints,
            foreignKeys,
            indexes);

        Assert.Equal(2, result.Count);
        var table = result[0];
        Assert.False(table.IsView);
        Assert.Equal(["Id", "Email"], table.Schema.Columns.Select(static column => column.Name));
        Assert.True(table.Schema.Columns[0].IsIdentity);
        Assert.Equal(200, table.Schema.Columns[1].Type.MaxLength);
        Assert.Equal("('')", table.Schema.Columns[1].DefaultExpression);
        Assert.Equal(["Id"], table.Schema.PrimaryKey!.Columns);
        Assert.True(table.Schema.PrimaryKey.IsClustered);
        Assert.Equal("UQ_Users_Email", Assert.Single(table.Schema.UniqueConstraints!).Name);
        Assert.Equal("Tenants", Assert.Single(table.Schema.ForeignKeys!).TargetTable);
        Assert.True(Assert.Single(table.Schema.Indexes!).IsUnique);

        var view = result[1];
        Assert.True(view.IsView);
        Assert.Empty(view.Schema.Columns);
        Assert.Null(view.Schema.PrimaryKey);
    }

    [Fact]
    public void SchemaSnapshotOptions_DefaultCompatLevel_UsesSharedConstant()
    {
        var options = new SchemaSnapshotOptions();

        Assert.Equal(SchemaSnapshotOptions.DefaultCompatLevel, options.CompatLevel);
    }
}
