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
2. **Parse preset JSON files**: Read all preset files in `rulesets/` directory:
   - `security-only.json`, `pragmatic.json`, `recommended.json`, `strict-logic.json`, `strict.json`
   - Each contains a `rules` array with `id` fields
3. **Assign importance tiers**: For each rule, determine its first-appearance tier:
   - If in security-only → **Critical**
   - Else if in pragmatic → **Essential**
   - Else if in recommended → **Recommended**
   - Else if in strict-logic → **Thorough**
   - Else if in strict → **Cosmetic**
4. **Compute statistics**:
   - Total rule count
   - Fixable rule count and percentage
   - Count per importance tier
   - Count per severity level (Error, Warning, Information)
   - Count per category
5. **Generate the file** following this structure:
   - Header with auto-generated notice
   - Table of Contents
   - **Rule Statistics** (total, fixable, by tier, by severity)
   - **Importance Tiers** (summary table with tier name, preset, count, cumulative, description)
   - **Rule Categories** (summary table with category name, count, description)
   - **Rules by Importance Tier** (primary grouping):
     - For each tier (Critical → Essential → Recommended → Thorough → Cosmetic):
       - Tier heading with count and description
       - Sub-grouped by category within the tier
       - Each category sub-group has a markdown table with: Rule ID, Description, Severity, Fixable
       - Rules sorted alphabetically by Rule ID within each category
       - Category order: Security, Safety, Correctness, Performance, Transactions, Schema, Style, Debug
   - **Rules by Severity** (Error, Warning, Information grouped bullet lists)
   - **Fixable Rules** (numbered list with fix command example)

### Semantic rule display names

Rules with IDs starting with `semantic-` should display as `semantic/xxx` (replacing the first hyphen after "semantic" with a slash). For example:
- Rule ID `semantic-duplicate-alias` → displays as `semantic/duplicate-alias`
- Doc link: `correctness/semantic-duplicate-alias.md` (uses hyphen in filename)

### Plural/singular rule counts

Use "1 rule" (singular) when a category subsection has exactly 1 rule, and "N rules" (plural) otherwise.

### Generator script

A Node.js generator script is available at `.claude/plans/generate-reference.js`. Run it with:

```bash
node .claude/plans/generate-reference.js
```

This reads all rule source files and preset JSONs, and writes the complete REFERENCE.md.

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
