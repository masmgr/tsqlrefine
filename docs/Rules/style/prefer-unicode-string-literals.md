# Prefer Unicode String Literals

**Rule ID:** `prefer-unicode-string-literals`
**Category:** Style
**Severity:** Information
**Fixable:** Yes

## Description

Encourages Unicode string literals (`N'...'`) instead of non-Unicode literals (`'...'`) to reduce encoding and collation issues.

## Rationale

Non-Unicode string literals can lead to data loss and inconsistent behavior when non-ASCII characters are involved. Using `N'...'` makes intent explicit and safer across environments. This rule uses conservative safe-mode autofixes to avoid risky behavior changes.

## Examples

### Bad

```sql
SELECT 'Hello';
SELECT '東京';
PRINT 'message';
```

### Good

```sql
SELECT N'Hello';
SELECT N'東京';
PRINT N'message';
```

## Safe Mode Notes

The autofix is skipped in contexts where changing literal encoding could be risky (e.g., assignments to `VARCHAR`/`CHAR`, explicit casts to non-Unicode types, hash functions, or unknown procedure/function parameters).

## Configuration

To disable this rule, add it to your `tsqlrefine.json`:

```json
{
  "ruleset": "custom-ruleset.json"
}
```

In `custom-ruleset.json`:

```json
{
  "rules": [
    { "id": "prefer-unicode-string-literals", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
