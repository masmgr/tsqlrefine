# String Assignment Length Mismatch

**Rule ID:** `string-assignment-length-mismatch`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects assignments whose statically known maximum string length exceeds the destination capacity.

## Rationale

Silent truncation, runtime truncation errors, and shortened identifiers often begin with inconsistent lengths between variables, procedure parameters, temporary storage, and persistent columns. This rule checks explicit INSERT column lists, UPDATE assignments, and SET/SELECT variable assignments.

The analysis follows same-batch variables, procedure parameters, temporary tables, table variables, and configured schema columns. It reports only when a safe upper bound can be derived from literals, references, casts, concatenation, `ISNULL`, `COALESCE`, `CONCAT`, or the `SUBSTRING`, `LEFT`, `RIGHT`, and `STUFF` slicing functions. Unknown expressions and assignments explicitly cast to fit are not reported.

## Examples

### Bad

```sql
DECLARE @short_code varchar(4);
SET @short_code = 'ABCDE';

CREATE TABLE #work (ShortName nvarchar(10), LongName nvarchar(50));
UPDATE #work SET ShortName = LongName;
```

### Good

```sql
DECLARE @code varchar(5);
SET @code = 'ABCDE';

DECLARE @short_code varchar(4);
SET @short_code = CAST(@source AS varchar(4));
```

## Configuration

```json
{
  "rules": {
    "string-assignment-length-mismatch": "none"
  }
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
