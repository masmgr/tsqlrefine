---
name: tsql-rule-implementer
description: Implement a new T-SQL linting rule end-to-end using TDD. Use when asked to create a new lint rule, add a detection rule, or implement a SQL analysis check.
---

# T-SQL Rule Implementer

Implement a new linting rule from scratch using TDD methodology.

## Workflow

1. **Understand the requirement**: Clarify what SQL pattern should be detected and why
2. **Check for existing rules**: Search `src/TsqlRefine.Rules/Rules/` to avoid duplicating existing rules
3. **Study a similar rule**: Read the most similar existing rule and its tests for patterns to follow
4. **Write tests first** in `tests/TsqlRefine.Rules.Tests/{Category}/{RuleName}RuleTests.cs`
   - Include SQL that should trigger violations (positive cases)
   - Include SQL that should NOT trigger violations (negative cases)
   - Check expected diagnostic count, message, and position
5. **Implement the rule** in `src/TsqlRefine.Rules/Rules/{Category}/{RuleName}Rule.cs`
   - Prefer AST-based (visitor pattern) over token-based
   - Use helpers from `src/TsqlRefine.Rules/Helpers/` — never duplicate common logic
   - See `.claude/rules/rules-development.md` for templates and helper reference
6. **Register the rule** in `src/TsqlRefine.Rules/BuiltinRuleProvider.cs`
7. **Add sample SQL** in `samples/sql/{rule-id}.sql`
8. **Run specific tests**: `dotnet test --filter "FullyQualifiedName~{RuleName}Tests"`
9. **Run full suite**: `dotnet test src/TsqlRefine.sln -c Release`
10. **Smoke test via CLI**: `echo "<test SQL>" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json`

## Naming

| Type | Pattern | Example |
|------|---------|---------|
| Rule class | PascalCase + "Rule" | `AvoidSelectStarRule` |
| Test class | PascalCase + "RuleTests" | `AvoidSelectStarRuleTests` |
| Rule ID | kebab-case | `avoid-select-star` |
| Visitor | Private nested + "Visitor" | `AvoidSelectStarVisitor` |

## Diagnostic Range

Diagnostics must point to the **narrowest relevant fragment**, not the entire statement:

- Statement-level: `ScriptDomHelpers.GetFirstTokenRange(node)` for the keyword only
- Specific keyword: `ScriptDomHelpers.FindKeywordTokenRange(node, TSqlTokenType.Xxx)`
- Sub-clause: pass the sub-fragment directly (e.g., `fragment: querySpec.TopRowFilter`)
- Expression: pass the specific expression (e.g., `fragment: comparison`)
- Name/alias: pass the identifier node (e.g., `fragment: namedTable.Alias`)
- **Never** pass an entire `QualifiedJoin`, `QuerySpecification`, `BinaryQueryExpression`, or `CommonTableExpression` as the diagnostic fragment

## Rules

- Always write tests before implementation (TDD)
- All tests must pass before considering the task done
- Use `FrozenSet`/`FrozenDictionary` for static lookup collections
