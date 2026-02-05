---
name: gen-rule-doc
description: Generate or update rule documentation for tsqlrefine. Use when: creating docs for new rules, updating existing rule docs, verifying docs are in sync with implementation, or regenerating docs/Rules/ content.
---

# Rule Documentation Generator

Generate rule documentation in `docs/Rules/`.

## Workflow

1. Read rule metadata from `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`
2. Extract: RuleId, Description, Category, DefaultSeverity, Fixable
3. Get examples from `tests/TsqlRefine.Rules.Tests/{RuleName}RuleTests.cs`
4. Generate markdown following template below
5. Save to `docs/Rules/{category}/{rule-id}.md`

## Template

```markdown
# {Rule Name}

**Rule ID:** `{rule-id}`
**Category:** {Category}
**Severity:** {Error|Warning|Information}
**Fixable:** {Yes|No}

## Description

{Description from metadata}

## Rationale

{Why this rule exists}

## Examples

### Bad

```sql
{SQL that triggers the rule}
```

### Good

```sql
{SQL that passes the rule}
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "{rule-id}", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
```

## Category Directories

| Category | Directory |
|----------|-----------|
| Correctness | `docs/Rules/correctness/` |
| Safety | `docs/Rules/safety/` |
| Security | `docs/Rules/security/` |
| Performance | `docs/Rules/performance/` |
| Style | `docs/Rules/style/` |
| Transactions | `docs/Rules/transactions/` |
| Schema | `docs/Rules/schema/` |
| Debug | `docs/Rules/debug/` |

## Commands

```powershell
# List all rules with metadata
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules --output json
```
