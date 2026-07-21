using System.Diagnostics.CodeAnalysis;
using System.Text;
using BenchmarkDotNet.Attributes;
using TsqlRefine.Core.Engine;
using TsqlRefine.Core.Model;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
[SuppressMessage(
    "Maintainability",
    "CA1506:Avoid excessive class coupling",
    Justification = "The benchmark intentionally exercises the complete schema processing pipeline.")]
public class SchemaBenchmarks
{
    private byte[] _snapshotJson = null!;
    private SchemaSnapshot _snapshot = null!;
    private SchemaProvider _provider = null!;
    private (ResolvedTable Table, string ColumnName)[] _resolutionRequests = null!;
    private TsqlRefineEngine _schemaRuleEngine = null!;
    private SqlInput[] _ruleInputs = null!;

    [GlobalSetup]
    public void Setup()
    {
        const int objectCount = 2500;
        const int columnsPerObject = 24;
        var tables = new TableSchema[objectCount];
        for (var tableIndex = 0; tableIndex < tables.Length; tableIndex++)
        {
            var columns = new ColumnSchema[columnsPerObject];
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                columns[columnIndex] = new ColumnSchema(
                    $"Column{columnIndex}",
                    new SqlTypeInfo("int", TypeCategory.ExactNumeric),
                    IsNullable: columnIndex != 0);
            }

            tables[tableIndex] = new TableSchema(
                "dbo",
                $"Table{tableIndex}",
                columns,
                new PrimaryKeyInfo(["Column0"], IsClustered: true));
        }

        var databases = new[] { new DatabaseSchema("BenchmarkDb", tables, []) };
        _snapshot = new SchemaSnapshot(
            new SnapshotMetadata("2026-01-01T00:00:00Z", "benchmark", "BenchmarkDb", 160, "benchmark")
            {
                DatabaseCollation = "Latin1_General_100_CI_AS",
                CollationLcid = 1033,
                CollationComparisonStyle = 1
            },
            databases);
        _snapshotJson = Encoding.UTF8.GetBytes(SchemaSnapshotSerializer.Serialize(_snapshot));
        _provider = new SchemaProvider(_snapshot);
        _resolutionRequests = new (ResolvedTable, string)[objectCount];
        for (var i = 0; i < objectCount; i++)
        {
            _resolutionRequests[i] = (_provider.ResolveTable(null, "dbo", $"Table{i}")!, $"Column{i % columnsPerObject}");
        }

        var schemaRuleIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "unresolved-table-reference",
            "unresolved-column-reference",
            "insert-column-not-in-table",
            "update-column-not-in-table",
            "delete-column-not-in-table",
            "join-foreign-key-mismatch",
            "update-join-cardinality-mismatch"
        };
        var schemaRules = new BuiltinRuleProvider().GetRules()
            .Where(rule => schemaRuleIds.Contains(rule.Metadata.RuleId))
            .ToArray();
        _schemaRuleEngine = new TsqlRefineEngine(schemaRules);
        _ruleInputs = [new SqlInput("schema-benchmark.sql", BuildRuleSql())];
    }

    [Benchmark]
    public SchemaSnapshot DeserializeJson()
    {
        using var stream = new MemoryStream(_snapshotJson, writable: false);
        return SchemaSnapshotSerializer.Deserialize(stream);
    }

    [Benchmark]
    public SchemaProvider ConstructSchemaProvider() => new(_snapshot);

    [Benchmark]
    public int ResolveTablesAndColumns()
    {
        var resolvedCount = 0;
        foreach (var (table, columnName) in _resolutionRequests)
        {
            if (_provider.ResolveColumn(table, columnName) is not null)
            {
                resolvedCount++;
            }
        }

        return resolvedCount;
    }

    [Benchmark]
    public LintResult RunSchemaRules() =>
        _schemaRuleEngine.Run(
            "lint",
            _ruleInputs,
            new EngineOptions(
                CompatLevel: 160,
                SchemaContext: new SchemaContext(_provider)));

    private static string BuildRuleSql()
    {
        var sql = new StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            sql.Append("SELECT a.Column0, b.Column1 FROM dbo.Table")
                .Append(i)
                .Append(" AS a INNER JOIN dbo.Table")
                .Append(i + 1)
                .AppendLine(" AS b ON a.Column0 = b.Column0;");
        }

        return sql.ToString();
    }
}
