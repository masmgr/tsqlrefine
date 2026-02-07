---
name: gen-rule-doc
description: Generate or update rule documentation. Use when asked to create docs for a new rule, update existing rule docs, or regenerate docs/Rules/ content.
---

# Rule Documentation Generator

Generate or update rule documentation in `docs/Rules/`.

## Workflow

1. Read rule metadata from `src/TsqlRefine.Rules/Rules/{Category}/{RuleName}Rule.cs`
   - Extract: RuleId, Description, Category, DefaultSeverity, Fixable
2. Read test file `tests/TsqlRefine.Rules.Tests/{Category}/{RuleName}RuleTests.cs`
   - Extract example SQL for good/bad cases
3. Determine output path: `docs/Rules/{category}/{rule-id}.md`
   - Categories: correctness, safety, security, performance, style, transactions, schema, debug
4. Generate markdown using the template below
5. Update `docs/Rules/REFERENCE.md` if a new rule was added
   - This file is auto-generated and contains all rule listings and statistics
   - Do not modify `docs/Rules/README.md` (manual overview/guide content)

## Regenerating REFERENCE.md

To fully regenerate `docs/Rules/REFERENCE.md` from scratch:

1. **Collect all rule metadata**: Read every `*Rule.cs` file under `src/TsqlRefine.Rules/Rules/` (recursively across all category and semantic subdirectories)
   - Extract: RuleId, Description, Category, DefaultSeverity, Fixable from each `RuleMetadata` constructor
2. **Compute statistics**:
   - Total rule count
   - Fixable rule count and percentage
   - Count per severity level (Error, Warning, Information)
3. **Build category table**: Count rules per category, add a short description for each
4. **Generate "Rules by Category" sections**: For each category, create a markdown table sorted alphabetically by Rule ID, linking to `{category}/{rule-id}.md`
5. **Generate "Rules by Severity" sections**: Group rules by severity, list as bullet points
6. **Generate "Fixable Rules" section**: List all fixable rules as numbered items
7. **Write the file** following the exact structure in the existing `docs/Rules/REFERENCE.md`:
   - Header with auto-generated notice
   - Table of Contents
   - Rule Statistics
   - Rule Categories (summary table)
   - Rules by Category (detailed tables per category)
   - Rules by Severity (grouped bullet lists)
   - Fixable Rules (numbered list with fix command example)

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
