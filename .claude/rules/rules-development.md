---
paths:
  - "src/TsqlRefine.Rules/**/*.cs"
  - "tests/TsqlRefine.Rules.Tests/**/*.cs"
---

# Rules Layer Development

Development patterns for the TsqlRefine.Rules project - T-SQL lint rules implementation.

## Adding a New Rule

### Step 1: Create Rule Class

Create `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`:

**AST-based rule (recommended for structural analysis):**
```csharp
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class MyRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "my-rule",
        Description: "Detects [issue description]",
        Category: "Performance",  // Safety|Correctness|Performance|Style|Security|Transactions|Schema|Debug
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new MyVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MyVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SomeStatementType node)
        {
            if (/* violation condition */)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Your diagnostic message",
                    code: "my-rule",
                    category: "Performance",
                    fixable: false
                );
            }
            base.ExplicitVisit(node);  // CRITICAL: Continue traversal
        }
    }
}
```

**Token-based rule (for pattern matching):**
```csharp
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class MyTokenRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "my-token-rule",
        Description: "...",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        for (var i = 0; i < context.Tokens.Count; i++)
        {
            if (TokenHelpers.IsKeyword(context.Tokens[i], "SELECT"))
            {
                if (TokenHelpers.IsTrivia(context.Tokens[i]))
                    continue;

                if (TokenHelpers.IsPrefixedByDot(context.Tokens, i))
                    continue;

                var start = context.Tokens[i].Start;
                var end = TokenHelpers.GetTokenEnd(context.Tokens[i]);

                yield return new Diagnostic(
                    Range: new TsqlRefine.PluginSdk.Range(start, end),
                    Message: "Your message",
                    Code: "my-token-rule",
                    Data: new DiagnosticData("my-token-rule", "Performance", false)
                );
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);
}
```

### Step 2: Register Rule

Add to `src/TsqlRefine.Rules/BuiltinRuleProvider.cs` in `GetRules()` array.

### Step 3: Add Tests

Create `tests/TsqlRefine.Rules.Tests/{RuleName}RuleTests.cs` with positive and negative cases.

### Step 4: Add Sample SQL

Create `samples/sql/{rule-id}.sql` demonstrating the violation.

## Helper Classes Reference

**Always use helper classes** - never duplicate common patterns.

### DiagnosticVisitorBase

Base class for AST visitors with automatic diagnostic collection:
```csharp
private sealed class MyVisitor : DiagnosticVisitorBase
{
    public override void ExplicitVisit(UpdateStatement node)
    {
        AddDiagnostic(
            fragment: node,
            message: "Message",
            code: "rule-id",
            category: "Category",
            fixable: false
        );
        base.ExplicitVisit(node);
    }
}
```

### TokenHelpers

Token stream analysis utilities:
- `IsKeyword(Token, string)`: Case-insensitive keyword matching
- `IsTrivia(Token)`: Detects whitespace and comments
- `IsPrefixedByDot(IReadOnlyList<Token>, int)`: Checks for qualified identifiers
- `GetTokenEnd(Token)`: Calculates token end position

### ScriptDomHelpers

AST fragment utilities:
- `GetRange(TSqlFragment)`: Converts ScriptDom coordinates (1-based) to PluginSdk Range (0-based)

### RuleHelpers

Common rule patterns:
- `NoFixes(RuleContext, Diagnostic)`: Standard implementation for non-fixable rules

### PredicateAwareVisitorBase

Visitor with predicate context tracking:
- Extends `DiagnosticVisitorBase` with `IsInPredicate` property
- Tracks WHERE, JOIN ON, and HAVING clause contexts

### TableReferenceHelpers

Table reference utilities:
- `CollectTableReferences(...)`: Recursively collects leaf table references from JOINs
- `CollectTableAliases(...)`: Collects all declared table aliases/names
- `GetAliasOrTableName(TableReference)`: Gets alias or base table name

### DatePartHelper

Date/time function detection:
- `IsDatePartFunction(FunctionCall)`: Checks for DATEADD, DATEDIFF, DATEPART, DATENAME
- `IsDatePartLiteralParameter(...)`: Detects datepart literal parameters

### ExpressionAnalysisHelpers

Expression analysis:
- `ContainsColumnReference(ScalarExpression)`: Checks if expression contains column references

### TextAnalysisHelpers

Raw text analysis:
- `SplitSqlLines(string)`: Splits SQL handling all line endings
- `CreateLineRangeDiagnostic(...)`: Creates diagnostics for specific lines

## Common AST Nodes

### DML Statements
- `UpdateStatement` - UPDATE (check `WhereClause`)
- `DeleteStatement` - DELETE (check `WhereClause`)
- `InsertStatement` - INSERT (check `InsertSpecification.Columns`)
- `SelectStatement` - SELECT

### Boolean Expressions
- `BooleanComparisonExpression` - Comparisons (=, <>, !=)
- `BooleanBinaryExpression` - AND/OR operations
- `BooleanIsNullExpression` - IS NULL / IS NOT NULL
- `NullLiteral` - NULL literal

### Table References
- `NamedTableReference` - Tables with optional hints
- `TableHint` - Table hints (check `HintKind`)

## Debugging Rules

Add breakpoints in:
- `TsqlRefineEngine.Run()`: Main execution loop
- `ScriptDomTokenizer.Analyze()`: Parsing stage
- Individual rule's `Analyze()` method

## Reference Files

- Pattern reference: `src/TsqlRefine.Rules/Rules/AvoidSelectStarRule.cs`
- Test reference: `tests/TsqlRefine.Rules.Tests/AvoidSelectStarRuleTests.cs`
- Helpers: `src/TsqlRefine.Rules/Helpers/`
