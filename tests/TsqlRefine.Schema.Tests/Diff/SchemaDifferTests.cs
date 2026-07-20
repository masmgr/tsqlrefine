using TsqlRefine.Schema.Diff;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Schema.Tests.Diff;

public sealed class SchemaDifferTests
{
    [Fact]
    public void Compare_RemovalTypeAndNullabilityChanges_ClassifiesBreakingChanges()
    {
        var before = TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int")
                .AddColumn("Email", "nvarchar", nullable: true, maxLength: 200)
                .AddColumn("Legacy", "int"))
            .Build();
        var after = TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int")
                .AddColumn("Email", "varchar", nullable: false, maxLength: 100))
            .Build();

        var result = SchemaDiffer.Compare(before, after);

        Assert.Equal(3, result.BreakingChangeCount);
        Assert.Contains(result.Changes, change =>
            change.Kind == SchemaChangeKind.ColumnRemoved && change.ColumnName == "Legacy" && change.IsBreaking);
        Assert.Contains(result.Changes, change =>
            change.Kind == SchemaChangeKind.ColumnTypeChanged && change.Before == "nvarchar(100)" && change.After == "varchar(100)");
        Assert.Contains(result.Changes, change =>
            change.Kind == SchemaChangeKind.ColumnNullabilityChanged && change.After == "not null" && change.IsBreaking);
    }

    [Fact]
    public void Compare_AdditionsAndNullableRelaxation_AreNonBreaking()
    {
        var before = TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table.AddColumn("Id", "int"))
            .Build();
        var after = TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int", nullable: true)
                .AddColumn("Name", "nvarchar", nullable: true, maxLength: 100))
            .AddTable("dbo", "Audit", table => table.AddColumn("Id", "int"))
            .Build();

        var result = SchemaDiffer.Compare(before, after);

        Assert.Equal(0, result.BreakingChangeCount);
        Assert.Equal(3, result.Changes.Count);
        Assert.All(result.Changes, static change => Assert.False(change.IsBreaking));
    }

    [Fact]
    public void Compare_NamesDifferOnlyByCase_ReturnsNoChanges()
    {
        var before = TestSchemaBuilder.Create("AppDb")
            .AddTable("dbo", "Users", table => table.AddColumn("Id", "int"))
            .Build();
        var after = TestSchemaBuilder.Create("appdb")
            .AddTable("DBO", "users", table => table.AddColumn("ID", "int"))
            .Build();

        var result = SchemaDiffer.Compare(before, after);

        Assert.Empty(result.Changes);
    }
}
