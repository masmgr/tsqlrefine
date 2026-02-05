---
name: gen-rule-doc
description: Generate or update rule documentation. Use when asked to create docs for a new rule, update existing rule docs, or regenerate docs/Rules/ content.
---

# Rule Documentation Generator

Generate or update rule documentation in `docs/Rules/`.

## Workflow

1. Read rule metadata from `src/TsqlRefine.Rules/Rules/{RuleName}Rule.cs`
   - Extract: RuleId, Description, Category, DefaultSeverity, Fixable
2. Read test file `tests/TsqlRefine.Rules.Tests/{RuleName}RuleTests.cs`
   - Extract example SQL for good/bad cases
3. Determine output path: `docs/Rules/{category}/{rule-id}.md`
   - Categories: correctness, safety, security, performance, style, transactions, schema, debug
4. Generate markdown using the template below
5. Update `docs/Rules/README.md` if a new rule was added

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

{Why this rule exists - infer from the detection logic}

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
