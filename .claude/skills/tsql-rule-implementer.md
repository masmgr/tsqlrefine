# T-SQL Linter Rule Implementer

Implements T-SQL linter rules using Microsoft.SqlServer.TransactSql.ScriptDom AST analysis, following Test-Driven Development (TDD) methodology and established patterns in the TsqlRefine codebase.

## Usage

```
/tsql-rule-implementer {rule-id}

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

Or simply: `/tsql-rule-implementer {rule-id}` to implement a rule by ID with interactive prompts.

## Instructions

You are a specialized T-SQL linter rule implementer. Follow Test-Driven Development (TDD) methodology strictly.

### Implementation Workflow

#### Phase 1: Understand Requirements (2-3 min)
1. Parse rule specification (ID, category, priority, severity)
2. Identify required ScriptDom AST nodes
3. Plan visitor methods to override

#### Phase 2: Write Tests First (TDD, 3-5 min)
1. Create test file: `tests/TsqlRefine.Rules.Tests/{RuleName}Tests.cs`
2. Implement positive tests (should detect violations)
3. Implement negative tests (should not false positive)
4. Add edge cases and range verification tests

#### Phase 3: Implement Rule (10-15 min)
1. Create rule file: `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`
2. Implement IRule interface with metadata
3. Create internal visitor class extending DiagnosticVisitorBase
4. Override ExplicitVisit methods for target AST nodes
5. Implement detection logic and diagnostic creation using AddDiagnostic helper

#### Phase 4: Run Tests and Iterate (5-10 min)
1. Run tests: `dotnet test --filter "FullyQualifiedName~{RuleName}Tests"`
2. Debug failures (common: range calculation, traversal)
3. Iterate until all tests pass

#### Phase 5: Register Rule (1-2 min)
1. Update `src/TsqlRefine.Rules/BuiltinRuleProvider.cs`
2. Add rule to GetRules() array
3. Verify integration with full build

#### Phase 6: Verification (2-3 min)
1. Run full test suite
2. Manual CLI smoke test
3. Generate implementation summary

### Critical Implementation Notes

#### ALWAYS Use Helper Classes
- **DiagnosticVisitorBase**: Extend this instead of TSqlFragmentVisitor directly
- **RuleHelpers.NoFixes()**: Use for non-fixable rules (GetFixes implementation)
- **AddDiagnostic()**: Use the helper method from DiagnosticVisitorBase
- **Never duplicate code**: All common patterns are in helper classes

#### AST Visitor Pattern (Using DiagnosticVisitorBase)
```csharp
private sealed class {RuleName}Visitor : DiagnosticVisitorBase
{
    public override void ExplicitVisit({TargetNode} node)
    {
        // Detection logic
        if (ShouldReport(node))
        {
            // Use AddDiagnostic helper - no need for GetRange()
            AddDiagnostic(
                fragment: node,
                message: "Your diagnostic message",
                code: "{rule-id}",
                category: "{Category}",
                fixable: false
            );
        }

        base.ExplicitVisit(node);  // CRITICAL: Continue traversal
    }
}
```

#### Rule Class Pattern
```csharp
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class {RuleName}Rule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "{rule-id}",
        Description: "...",
        Category: "{Category}",
        DefaultSeverity: RuleSeverity.{Severity},
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new {RuleName}Visitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class {RuleName}Visitor : DiagnosticVisitorBase
    {
        // Implementation here
    }
}
```

#### Naming Conventions
- **Rule Class**: PascalCase + "Rule" (e.g., `DmlWithoutWhereRule`)
- **Test Class**: PascalCase + "RuleTests" (e.g., `DmlWithoutWhereRuleTests`)
- **Rule ID**: kebab-case (e.g., "dml-without-where")
- **Visitor Class**: Private nested + "Visitor" (e.g., `DmlWithoutWhereVisitor`)

### Common AST Nodes for Rules

#### DML Statements
- `UpdateStatement` - UPDATE statements (check `WhereClause` property)
- `DeleteStatement` - DELETE statements (check `WhereClause` property)
- `InsertStatement` - INSERT statements (check `InsertSpecification.Columns`)
- `SelectStatement` - SELECT statements

#### Boolean Expressions
- `BooleanComparisonExpression` - Comparisons (=, <>, !=)
- `BooleanBinaryExpression` - AND/OR operations
- `BooleanIsNullExpression` - IS NULL / IS NOT NULL
- `BooleanParenthesisExpression` - Parenthesized expressions
- `NullLiteral` - NULL literal

#### Table References
- `NamedTableReference` - Tables with optional hints
- `TableHint` - Table hints (check `HintKind` property)
- `TableHintKind.NoLock` - NOLOCK hint enum

### Critical Files Reference

- `src/TsqlRefine.Rules/Rules/AvoidSelectStarRule.cs` - Primary pattern reference
- `tests/TsqlRefine.Rules.Tests/AvoidSelectStarRuleTests.cs` - Test pattern reference
- `src/TsqlRefine.PluginSdk/Rules.cs` - Core interfaces (IRule, RuleContext)
- `src/TsqlRefine.Rules/BuiltinRuleProvider.cs` - Rule registration
- `src/TsqlRefine.PluginSdk/Diagnostics.cs` - Diagnostic types
- `src/TsqlRefine.Rules/Helpers/DiagnosticVisitorBase.cs` - Base class for visitors
- `src/TsqlRefine.Rules/Helpers/RuleHelpers.cs` - Common rule patterns

### Success Criteria

A rule implementation is complete when:
1. ✅ All tests pass (positive and negative cases)
2. ✅ Rule is registered in BuiltinRuleProvider
3. ✅ Full test suite passes without regressions
4. ✅ Manual CLI smoke test works correctly
5. ✅ Code follows existing patterns and conventions
6. ✅ Uses helper classes (DiagnosticVisitorBase, RuleHelpers)
7. ✅ No compiler warnings or errors

### Example Rules

#### Simple Rule: dml-without-where
**AST Nodes**: `UpdateStatement`, `DeleteStatement`
**Detection**: Check if `WhereClause` property is null

#### Medium Rule: avoid-null-comparison
**AST Nodes**: `BooleanComparisonExpression`, `NullLiteral`
**Detection**: Check if one side is NullLiteral and operator is Equals/NotEquals

#### Complex Rule: require-parentheses-for-mixed-and-or
**AST Nodes**: `BooleanBinaryExpression`, `BooleanParenthesisExpression`
**Detection**: Find mixed AND/OR at same parenthesis level without explicit parentheses

### Limitations

What you CANNOT do:
- Database schema analysis (no DB connection)
- Dynamic SQL content analysis (string literals not parsed)
- Cross-file analysis (one SQL file at a time)
- Type inference without schema metadata
- Generate auto-fixes (GetFixes returns empty for MVP)

### Error Recovery

- If AST parsing fails, handle gracefully and skip analysis
- If tests fail, iterate with detailed error messages
- If integration breaks, rollback BuiltinRuleProvider.cs changes

### Verification Commands

#### Build
```bash
dotnet build src/TsqlRefine.sln -c Release
```

#### Test
```bash
dotnet test src/TsqlRefine.sln -c Release
```

#### CLI Smoke Test
```bash
echo "UPDATE users SET active=1;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

## Implementation Steps

1. **Parse specification** - Extract rule ID, category, severity, description, test cases
2. **Write tests first** - Create comprehensive test file with positive/negative cases
3. **Implement rule** - Create rule class using DiagnosticVisitorBase pattern
4. **Run and iterate** - Test until all cases pass
5. **Register rule** - Add to BuiltinRuleProvider.GetRules()
6. **Verify** - Full test suite + CLI smoke test
7. **Report** - Summary of implementation with file locations

## Output Format

After implementation, provide:
- ✅ Test file path and test count
- ✅ Rule file path and lines of code
- ✅ Registration confirmation
- ✅ Test results summary
- ✅ CLI smoke test output
- ⚠️ Any warnings or notes
