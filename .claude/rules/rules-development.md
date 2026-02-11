---
paths:
  - "src/TsqlRefine.Rules/**/*.cs"
  - "tests/TsqlRefine.Rules.Tests/**/*.cs"
---

# Rules Layer Development

Development patterns for the TsqlRefine.Rules project - T-SQL lint rules implementation.

## Adding a New Rule

### Step 1: Create Rule Class

Create `src/TsqlRefine.Rules/Rules/{Category}/{RuleName}Rule.cs`:

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

Create `tests/TsqlRefine.Rules.Tests/{Category}/{RuleName}RuleTests.cs` with positive and negative cases.

### Step 4: Add Sample SQL

Create `samples/sql/{rule-id}.sql` demonstrating the violation.

## Helper Classes Reference

**Always use helper classes** - never duplicate common patterns. Helpers are organized into subdirectories:

```
Helpers/
├── Analysis/          # Expression and data analysis
│   ├── DatePartHelper.cs
│   ├── ExpressionAnalysisHelpers.cs
│   └── SqlDataTypeHelpers.cs
├── Diagnostics/       # Diagnostic creation utilities
│   ├── BeginEndHelpers.cs
│   ├── RuleHelpers.cs
│   └── ScriptDomHelpers.cs
├── Scope/             # Alias and reference tracking
│   ├── AliasScopeManager.cs
│   ├── ColumnReferenceHelpers.cs
│   └── TableReferenceHelpers.cs
├── Tokens/            # Token stream utilities
│   ├── TextAnalysisHelpers.cs
│   ├── TextPositionHelpers.cs
│   └── TokenHelpers.cs
└── Visitors/          # AST visitor base classes
    ├── DiagnosticVisitorBase.cs
    ├── PredicateAwareVisitorBase.cs
    ├── ScopeBlockingVisitor.cs
    └── ScopeDelegatingVisitor.cs
```

### Visitors/DiagnosticVisitorBase

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

### Visitors/PredicateAwareVisitorBase

Visitor with predicate context tracking:
- Extends `DiagnosticVisitorBase` with `IsInPredicate` property
- Tracks WHERE, JOIN ON, and HAVING clause contexts

### Visitors/ScopeBlockingVisitor & ScopeDelegatingVisitor

Advanced visitors for scope-aware traversal (subquery boundaries, CTE scopes).

### Tokens/TokenHelpers

Token stream analysis utilities:
- `IsKeyword(Token, string)`: Case-insensitive keyword matching
- `IsTrivia(Token)`: Detects whitespace and comments
- `IsPrefixedByDot(IReadOnlyList<Token>, int)`: Checks for qualified identifiers
- `GetTokenEnd(Token)`: Calculates token end position

### Tokens/TextAnalysisHelpers

Raw text analysis:
- `SplitSqlLines(string)`: Splits SQL handling all line endings
- `CreateLineRangeDiagnostic(...)`: Creates diagnostics for specific lines

### Tokens/TextPositionHelpers

Position calculation utilities for token-based rules.

### Diagnostics/ScriptDomHelpers

AST fragment utilities:
- `GetRange(TSqlFragment)`: Converts ScriptDom coordinates (1-based) to PluginSdk Range (0-based)
- `GetFirstTokenRange(TSqlFragment)`: Returns range for the first token only (e.g., statement keyword)
- `FindKeywordTokenRange(TSqlFragment, TSqlTokenType)`: Searches for a specific token type within the fragment and returns its range; falls back to `GetFirstTokenRange`

### Diagnostics/RuleHelpers

Common rule patterns:
- `NoFixes(RuleContext, Diagnostic)`: Standard implementation for non-fixable rules

### Diagnostics/BeginEndHelpers

BEGIN/END block detection and analysis utilities.

### Scope/TableReferenceHelpers

Table reference utilities:
- `CollectTableReferences(...)`: Recursively collects leaf table references from JOINs
- `CollectTableAliases(...)`: Collects all declared table aliases/names
- `GetAliasOrTableName(TableReference)`: Gets alias or base table name

### Scope/ColumnReferenceHelpers

Column reference resolution and analysis.

### Scope/AliasScopeManager

Tracks alias declarations and validates scope visibility.

### Analysis/DatePartHelper

Date/time function detection:
- `IsDatePartFunction(FunctionCall)`: Checks for DATEADD, DATEDIFF, DATEPART, DATENAME
- `IsDatePartLiteralParameter(...)`: Detects datepart literal parameters

### Analysis/ExpressionAnalysisHelpers

Expression analysis:
- `ContainsColumnReference(ScalarExpression)`: Checks if expression contains column references

### Analysis/SqlDataTypeHelpers

SQL data type classification and compatibility checks.

## Diagnostic Range Guidelines

Diagnostics must point to the **narrowest relevant fragment**, not the entire statement. Broad ranges make it hard to locate issues in long multi-line SQL.

### Range selection by rule type

| Scenario | Target | Helper / Approach |
|----------|--------|-------------------|
| Statement-level violation (e.g., UPDATE without WHERE) | Statement keyword only | `ScriptDomHelpers.GetFirstTokenRange(node)` |
| Specific keyword violation (e.g., SELECT DISTINCT) | The keyword itself | `ScriptDomHelpers.FindKeywordTokenRange(node, TSqlTokenType.Distinct)` |
| Sub-clause violation (e.g., TOP without ORDER BY) | The sub-clause fragment | `AddDiagnostic(fragment: querySpec.TopRowFilter, ...)` |
| Expression-level violation (e.g., `col = NULL`) | The expression itself | `AddDiagnostic(fragment: comparison, ...)` |
| Column reference issue (e.g., unqualified column) | The `ColumnReferenceExpression` | `AddDiagnostic(fragment: colRef, ...)` |
| Name/alias conflict | The identifier or alias node | `AddDiagnostic(fragment: namedTable.Alias, ...)` |
| JOIN condition issue (e.g., always-true ON clause) | The condition expression | `AddDiagnostic(fragment: comparison, ...)` not the `QualifiedJoin` |

### Anti-patterns to avoid

- **Never** use `fragment: node` where `node` is a full `QualifiedJoin` — narrow to `SearchCondition`, `SecondTableReference`, or the specific problematic expression
- **Never** use `fragment: node` where `node` is a full `QuerySpecification` or `BinaryQueryExpression` — narrow to the specific keyword, clause, or column
- **Never** use `fragment: node` where `node` is a full `CommonTableExpression` — narrow to `ExpressionName`

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

- Pattern reference: `src/TsqlRefine.Rules/Rules/Performance/AvoidSelectStarRule.cs`
- Test reference: `tests/TsqlRefine.Rules.Tests/Performance/AvoidSelectStarRuleTests.cs`
- Helpers: `src/TsqlRefine.Rules/Helpers/` (Analysis, Diagnostics, Scope, Tokens, Visitors)
