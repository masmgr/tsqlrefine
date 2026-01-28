using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class AvoidHeapTableRuleTests
{
    private readonly AvoidHeapTableRule _rule = new();

    [Theory]
    [InlineData("CREATE TABLE users (id INT, name VARCHAR(50));")]
    [InlineData("CREATE TABLE products (id INT, description VARCHAR(100), price DECIMAL(10,2));")]
    [InlineData(@"CREATE TABLE orders (
        order_id INT NOT NULL,
        customer_id INT,
        order_date DATE
    );")]
    public void Analyze_TableWithoutClusteredIndex_ReturnsDiagnostic(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-heap-table", diagnostics[0].Code);
        Assert.Contains("heap", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CREATE TABLE users (id INT PRIMARY KEY CLUSTERED, name VARCHAR(50));")]
    [InlineData(@"CREATE TABLE products (
        id INT,
        name VARCHAR(50),
        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (id)
    );")]
    [InlineData(@"CREATE TABLE orders (
        id INT,
        date DATE,
        INDEX IX_Orders CLUSTERED (id)
    );")]
    public void Analyze_TableWithClusteredIndex_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithClusteredColumnStore_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE events (
                id INT,
                timestamp DATETIME,
                data VARCHAR(MAX),
                INDEX IX_Events CLUSTERED COLUMNSTORE
            );";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TableWithNonClusteredIndexOnly_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE logs (
                id INT,
                message VARCHAR(MAX),
                INDEX IX_Logs NONCLUSTERED (id)
            );";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-heap-table", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleTablesWithAndWithoutClusteredIndex_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE users (id INT PRIMARY KEY CLUSTERED, name VARCHAR(50));
            CREATE TABLE logs (id INT, message VARCHAR(MAX));
            CREATE TABLE products (id INT, price DECIMAL);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);  // logs and products are heaps
        Assert.All(diagnostics, d => Assert.Equal("avoid-heap-table", d.Code));
    }

    [Fact]
    public void Analyze_TableWithPrimaryKeyButNotClustered_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE users (
                id INT PRIMARY KEY NONCLUSTERED,
                name VARCHAR(50)
            );";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-heap-table", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_TableWithConstraintClusteredPrimaryKey_ReturnsEmpty()
    {
        // Arrange
        const string sql = @"
            CREATE TABLE orders (
                order_id INT NOT NULL,
                customer_id INT NOT NULL,
                CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (order_id, customer_id)
            );";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("CREATE TABLE #temp (id INT, name VARCHAR(50));")]
    [InlineData("CREATE TABLE ##global_temp (id INT, description VARCHAR(100));")]
    [InlineData(@"CREATE TABLE #orders (
        order_id INT NOT NULL,
        customer_id INT,
        order_date DATE
    );")]
    public void Analyze_TemporaryTableWithoutClusteredIndex_ReturnsEmpty(string sql)
    {
        // Arrange
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-heap-table", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
        Assert.Contains("heap", _rule.Metadata.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFixes_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("CREATE TABLE users (id INT, name VARCHAR(50));");
        var diagnostic = new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(new Position(0, 0), new Position(0, 10)),
            Message: "test",
            Code: "avoid-heap-table"
        );

        // Act
        var fixes = _rule.GetFixes(context, diagnostic).ToArray();

        // Assert
        Assert.Empty(fixes);
    }

    private static RuleContext CreateContext(string sql, int compatLevel = 150)
    {
        return RuleTestContext.CreateContext(sql, compatLevel);
    }
}
