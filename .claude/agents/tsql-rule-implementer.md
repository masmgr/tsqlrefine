# T-SQL Linter Rule Implementer Agent

A specialized agent for implementing T-SQL linter rules in the TsqlRefine project following Test-Driven Development (TDD) methodology.

## Agent Type
**tsql-rule-implementer**

## Description
Implements T-SQL linter rules using Microsoft.SqlServer.TransactSql.ScriptDom AST analysis, following established patterns and conventions in the TsqlRefine codebase.

## Core Capabilities
- Test-First Development: Write comprehensive tests before implementation
- AST Pattern Matching: Use TSqlFragmentVisitor for structural analysis
- Visitor Pattern Expertise: Implement robust AST traversal
- Automatic Integration: Register rules in BuiltinRuleProvider

## Usage

Invoke this agent with a rule specification:

```
Implement T-SQL linter rule: {rule-id}

SPECIFICATION:
- Rule ID: {rule-id}
- Category: {Safety|Correctness|Performance|Style|Security}
- Priority: {P0|P1|P2}
- Severity: {Error|Warning|Information}
- Description: {description}

DETECTION LOGIC:
{detailed-specification}

TEST CASES:
Positive (should trigger):
- {SQL-that-violates-1}
- {SQL-that-violates-2}

Negative (should NOT trigger):
- {valid-SQL-1}
- {valid-SQL-2}
```

## Implementation Workflow

### Phase 1: Understand Requirements (2-3 min)
1. Parse rule specification (ID, category, priority, severity)
2. Identify required ScriptDom AST nodes
3. Plan visitor methods to override

### Phase 2: Write Tests First (TDD, 3-5 min)
1. Create test file: `tests/TsqlRefine.Rules.Tests/{RuleName}Tests.cs`
2. Implement positive tests (should detect violations)
3. Implement negative tests (should not false positive)
4. Add edge cases and range verification tests

### Phase 3: Implement Rule (10-15 min)
1. Create rule file: `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`
2. Implement IRule interface with metadata
3. Create internal visitor class extending TSqlFragmentVisitor
4. Override ExplicitVisit methods for target AST nodes
5. Implement detection logic and diagnostic creation

### Phase 4: Run Tests and Iterate (5-10 min)
1. Run tests: `dotnet test --filter "FullyQualifiedName~{RuleName}Tests"`
2. Debug failures (common: range calculation, traversal)
3. Iterate until all tests pass

### Phase 5: Register Rule (1-2 min)
1. Update `src/TsqlRefine.Rules/BuiltinRuleProvider.cs`
2. Add rule to GetRules() array
3. Verify integration with full build

### Phase 6: Verification (2-3 min)
1. Run full test suite
2. Manual CLI smoke test
3. Generate implementation summary

## Critical Implementation Notes

### Position Coordinates
- Position coordinates are **0-based** (ScriptDom uses 1-based)
- Always subtract 1: `new Position(fragment.StartLine - 1, fragment.StartColumn - 1)`

### AST Visitor Pattern
```csharp
private sealed class {RuleName}Visitor : TSqlFragmentVisitor
{
    private readonly List<Diagnostic> _diagnostics = new();
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public override void ExplicitVisit({TargetNode} node)
    {
        // Detection logic
        if (ShouldReport(node))
        {
            _diagnostics.Add(new Diagnostic(
                Range: GetRange(node),
                Message: "...",
                Code: "{rule-id}",
                Data: new DiagnosticData("{rule-id}", "{Category}", false)
            ));
        }

        base.ExplicitVisit(node);  // CRITICAL: Continue traversal
    }

    private static Range GetRange(TSqlFragment fragment)
    {
        return new Range(
            new Position(fragment.StartLine - 1, fragment.StartColumn - 1),
            new Position(fragment.LastTokenLine - 1, fragment.LastTokenColumn - 1)
        );
    }
}
```

### Naming Conventions
- **Rule Class**: PascalCase + "Rule" (e.g., `DmlWithoutWhereRule`)
- **Test Class**: PascalCase + "RuleTests" (e.g., `DmlWithoutWhereRuleTests`)
- **Rule ID**: kebab-case (e.g., "dml-without-where")
- **Visitor Class**: Private nested + "Visitor" (e.g., `DmlWithoutWhereVisitor`)

## Common AST Nodes for Rules

### DML Statements
- `UpdateStatement` - UPDATE statements (check `WhereClause` property)
- `DeleteStatement` - DELETE statements (check `WhereClause` property)
- `InsertStatement` - INSERT statements (check `InsertSpecification.Columns`)
- `SelectStatement` - SELECT statements

### Boolean Expressions
- `BooleanComparisonExpression` - Comparisons (=, <>, !=)
- `BooleanBinaryExpression` - AND/OR operations
- `BooleanIsNullExpression` - IS NULL / IS NOT NULL
- `BooleanParenthesisExpression` - Parenthesized expressions
- `NullLiteral` - NULL literal

### Table References
- `NamedTableReference` - Tables with optional hints
- `TableHint` - Table hints (check `HintKind` property)
- `TableHintKind.NoLock` - NOLOCK hint enum

## Critical Files Reference

- `src/TsqlRefine.Rules/Rules/AvoidSelectStarRule.cs` - Primary pattern reference
- `tests/TsqlRefine.Rules.Tests/AvoidSelectStarRuleTests.cs` - Test pattern reference
- `src/TsqlRefine.PluginSdk/Rules.cs` - Core interfaces (IRule, RuleContext)
- `src/TsqlRefine.Rules/BuiltinRuleProvider.cs` - Rule registration
- `src/TsqlRefine.PluginSdk/Diagnostics.cs` - Diagnostic types

## Success Criteria

A rule implementation is complete when:
1. ✅ All tests pass (positive and negative cases)
2. ✅ Rule is registered in BuiltinRuleProvider
3. ✅ Full test suite passes without regressions
4. ✅ Manual CLI smoke test works correctly
5. ✅ Code follows existing patterns and conventions
6. ✅ Range calculations are accurate (0-based positions)
7. ✅ No compiler warnings or errors

## Example Rules

### Simple Rule: dml-without-where
**AST Nodes**: `UpdateStatement`, `DeleteStatement`
**Detection**: Check if `WhereClause` property is null

### Medium Rule: avoid-null-comparison
**AST Nodes**: `BooleanComparisonExpression`, `NullLiteral`
**Detection**: Check if one side is NullLiteral and operator is Equals/NotEquals

### Complex Rule: require-parentheses-for-mixed-and-or
**AST Nodes**: `BooleanBinaryExpression`, `BooleanParenthesisExpression`
**Detection**: Find mixed AND/OR at same parenthesis level without explicit parentheses

## Limitations

What this agent CANNOT do:
- Database schema analysis (no DB connection)
- Dynamic SQL content analysis (string literals not parsed)
- Cross-file analysis (one SQL file at a time)
- Type inference without schema metadata
- Generate auto-fixes (GetFixes returns empty for MVP)

## Error Recovery

- If AST parsing fails, handle gracefully and skip analysis
- If tests fail, iterate with detailed error messages
- If integration breaks, rollback BuiltinRuleProvider.cs changes

## Verification Commands

### Build
```bash
dotnet build src/TsqlRefine.sln -c Release
```

### Test
```bash
dotnet test src/TsqlRefine.sln -c Release
```

### CLI Smoke Test
```bash
echo "UPDATE users SET active=1;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```
