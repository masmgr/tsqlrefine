# Mixed String Length Functions In Loop

**Rule ID:** `mixed-string-length-functions-in-loop`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects WHILE loops that measure a string's remaining data with `DATALENGTH` but advance a slicing expression for the same variable with `LEN`.

## Rationale

`DATALENGTH` counts bytes while `LEN` counts characters and excludes trailing spaces. Mixing them in a loop termination test and its progress calculation can leave data unconsumed, skip characters, or prevent termination. Use one consistent unit and account explicitly for Unicode byte width when necessary.

The rule recognizes progress through `SUBSTRING`, `LEFT`, `RIGHT`, and `STUFF`. Nested WHILE statements are analyzed independently.

## Examples

### Bad

```sql
WHILE DATALENGTH(@remaining) > 0
BEGIN
    SET @remaining = SUBSTRING(@remaining, LEN(@chunk) + 1, 8000);
END;
```

### Good

```sql
WHILE LEN(@remaining) > 0
BEGIN
    SET @remaining = SUBSTRING(@remaining, LEN(@chunk) + 1, 8000);
END;
```

## Configuration

```json
{
  "rules": {
    "mixed-string-length-functions-in-loop": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
