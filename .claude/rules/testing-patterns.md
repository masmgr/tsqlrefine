---
paths:
  - "tests/**/*.cs"
---

# Testing Patterns

Development patterns for all test projects in TsqlRefine.

## Test Project Structure

```
tests/
├── TsqlRefine.Core.Tests/          # Engine and tokenizer tests
├── TsqlRefine.Rules.Tests/         # Rule and helper tests
│   ├── Correctness/                # Correctness rule tests
│   ├── Debug/                      # Debug rule tests
│   ├── Performance/                # Performance rule tests
│   ├── Safety/                     # Safety rule tests
│   ├── Schema/                     # Schema rule tests
│   ├── Security/                   # Security rule tests
│   ├── Style/                      # Style rule tests
│   ├── Transactions/               # Transaction rule tests
│   └── Helpers/                    # Helper utility tests
├── TsqlRefine.Cli.Tests/           # CLI integration tests
└── TsqlRefine.Formatting.Tests/    # Formatter tests
    └── Helpers/                    # Formatting helper tests
```

## xUnit Conventions

All tests use xUnit with these patterns:

### Test Class Naming

- Rule tests: `{RuleName}RuleTests` (e.g., `AvoidSelectStarRuleTests`)
- Helper tests: `{HelperName}Tests` (e.g., `TokenHelpersTests`)
- Integration tests: `{Feature}IntegrationTests`

### Test Method Naming

Use pattern: `{Method}_{Scenario}_{ExpectedResult}`

```csharp
[Fact]
public void Analyze_SelectStar_ReturnsDiagnostic()

[Fact]
public void Analyze_ExplicitColumns_ReturnsEmpty()

[Fact]
public void Transform_ProtectedString_PreservesContent()
```

## Rule Testing Patterns

### Basic Rule Test

```csharp
public class MyRuleTests
{
    private readonly MyRule _rule = new();

    [Fact]
    public void Analyze_ViolationCase_ReturnsDiagnostic()
    {
        // Arrange
        var sql = "SELECT * FROM users";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToList();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("my-rule", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ValidCase_ReturnsEmpty()
    {
        var sql = "SELECT id, name FROM users";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToList();

        Assert.Empty(diagnostics);
    }

    private static RuleContext CreateContext(string sql)
    {
        var tokenizer = new ScriptDomTokenizer();
        var result = tokenizer.Analyze(sql, 150);
        return new RuleContext("test.sql", 150, result.Ast, result.Tokens, RuleSettings.Default);
    }
}
```

### Diagnostic Range Verification

```csharp
[Fact]
public void Analyze_Violation_ReportsCorrectRange()
{
    var sql = "SELECT * FROM users";
    var context = CreateContext(sql);

    var diagnostics = _rule.Analyze(context).ToList();

    var diagnostic = Assert.Single(diagnostics);
    Assert.Equal(0, diagnostic.Range.Start.Line);
    Assert.Equal(7, diagnostic.Range.Start.Character);  // Position of '*'
}
```

### Multiple Violations

```csharp
[Theory]
[InlineData("SELECT * FROM a; SELECT * FROM b", 2)]
[InlineData("SELECT * FROM a", 1)]
[InlineData("SELECT id FROM a", 0)]
public void Analyze_MultipleStatements_ReturnsExpectedCount(string sql, int expected)
{
    var context = CreateContext(sql);
    var diagnostics = _rule.Analyze(context).ToList();
    Assert.Equal(expected, diagnostics.Count);
}
```

## Helper Testing Patterns

### TokenHelpers Tests

```csharp
[Fact]
public void IsKeyword_MatchingKeyword_ReturnsTrue()
{
    var token = new Token(TSqlTokenType.Select, "SELECT", new Position(0, 0));
    Assert.True(TokenHelpers.IsKeyword(token, "SELECT"));
}

[Fact]
public void IsKeyword_CaseInsensitive_ReturnsTrue()
{
    var token = new Token(TSqlTokenType.Select, "select", new Position(0, 0));
    Assert.True(TokenHelpers.IsKeyword(token, "SELECT"));
}
```

### Formatting Helper Tests

```csharp
[Fact]
public void Transform_BasicInput_ReturnsExpected()
{
    var input = "select * from users";
    var options = new FormattingOptions();

    var result = MyHelper.Transform(input, options);

    Assert.Equal("SELECT * FROM users", result);
}

[Fact]
public void Transform_ProtectedContent_PreservesStrings()
{
    var input = "SELECT 'select * from' FROM users";
    var options = new FormattingOptions();

    var result = MyHelper.Transform(input, options);

    Assert.Equal("SELECT 'select * from' FROM users", result);
}
```

## CLI Integration Tests

```csharp
public class LintCommandTests
{
    [Fact]
    public async Task Lint_FileWithViolation_ReturnsExitCode1()
    {
        var tempFile = CreateTempFile("SELECT * FROM users");

        var result = await CliApp.RunAsync(new[] { "lint", tempFile });

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Lint_CleanFile_ReturnsExitCode0()
    {
        var tempFile = CreateTempFile("SELECT id FROM users");

        var result = await CliApp.RunAsync(new[] { "lint", tempFile });

        Assert.Equal(0, result);
    }
}
```

## Running Tests

```powershell
# Run all tests
dotnet test src/TsqlRefine.sln -c Release

# Run specific project tests
dotnet test tests/TsqlRefine.Rules.Tests -c Release

# Run single test by name
dotnet test --filter "FullyQualifiedName~MyTestName"

# Run tests with verbose output
dotnet test -c Release --logger "console;verbosity=detailed"
```

## Test Categories

Use traits for categorization when needed:

```csharp
[Trait("Category", "Integration")]
[Fact]
public async Task Lint_Integration_WorksEndToEnd()

[Trait("Category", "Slow")]
[Fact]
public void LargeFile_Performance_CompletesInTime()
```

## Common Test Utilities

### Creating Test Context

```csharp
private static RuleContext CreateContext(string sql, int compatLevel = 150)
{
    var tokenizer = new ScriptDomTokenizer();
    var result = tokenizer.Analyze(sql, compatLevel);
    return new RuleContext("test.sql", compatLevel, result.Ast, result.Tokens, RuleSettings.Default);
}
```

### Temporary File Helpers

```csharp
private static string CreateTempFile(string content)
{
    var path = Path.GetTempFileName() + ".sql";
    File.WriteAllText(path, content);
    return path;
}
```

## Test Coverage Expectations

- **Rules**: Both positive (violation) and negative (valid) cases
- **Helpers**: Edge cases, null handling, multi-line content
- **CLI**: Exit codes, output formats, error handling
- **Formatting**: Protected regions, various input patterns
