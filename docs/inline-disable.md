# Inline Disable Directives

tsqlrefine allows you to temporarily disable rules using comments within SQL files.

---

## Basic Syntax

### Disable All Rules

```sql
/* tsqlrefine-disable */
SELECT * FROM Users;
/* tsqlrefine-enable */
```

All rule violations within the range from `tsqlrefine-disable` to `tsqlrefine-enable` are suppressed.

### Disable Specific Rules

```sql
/* tsqlrefine-disable avoid-select-star */
SELECT * FROM Users;
/* tsqlrefine-enable avoid-select-star */
```

Only the specified rule ID is disabled. Other rules continue to apply.

### Disable Multiple Rules

```sql
/* tsqlrefine-disable avoid-select-star, dml-without-where */
SELECT * FROM Users;
UPDATE Users SET Status = 1;
/* tsqlrefine-enable avoid-select-star, dml-without-where */
```

Multiple rule IDs can be specified separated by commas.

### Reason Text

You can add a reason after a colon (`:`) to explain why rules are disabled:

```sql
/* tsqlrefine-disable avoid-select-star: legacy view depends on column order */
SELECT * FROM LegacyView;
/* tsqlrefine-enable avoid-select-star */
```

The reason is separated from rule IDs by the first colon. Multiple rules with a reason:

```sql
/* tsqlrefine-disable avoid-select-star, dml-without-where: known migration pattern */
```

Disable all rules with a reason:

```sql
-- tsqlrefine-disable: this section is auto-generated code
```

Reason text is purely informational and does not affect suppression behavior.

---

## Disable for Entire Script

Placing `tsqlrefine-disable` at the beginning of a file and omitting the corresponding `tsqlrefine-enable` disables rules for the entire script.

### Disable All Rules (Entire Script)

```sql
/* tsqlrefine-disable */

SELECT * FROM Users;
UPDATE Users SET Status = 1;
DELETE FROM TempData;
```

### Disable Specific Rules (Entire Script)

```sql
/* tsqlrefine-disable avoid-select-star */

SELECT * FROM Users;
SELECT * FROM Orders;
```

---

## Supported Comment Formats

### Block Comments

```sql
/* tsqlrefine-disable */
/* tsqlrefine-enable */
```

### Line Comments

```sql
-- tsqlrefine-disable
-- tsqlrefine-enable
```

---

## Directive Characteristics

### Case Insensitive

Directive names are case insensitive.

```sql
/* TSQLREFINE-DISABLE */
/* TsqlRefine-Disable */
/* tsqlrefine-disable */
```

All of the above behave the same.

### Rule ID Case

Rule IDs are also case insensitive.

```sql
/* tsqlrefine-disable AVOID-SELECT-STAR */
/* tsqlrefine-disable Avoid-Select-Star */
/* tsqlrefine-disable avoid-select-star */
```

### Whitespace Handling

Whitespace around directives is ignored.

```sql
/*tsqlrefine-disable*/
/*  tsqlrefine-disable  */
/* 	tsqlrefine-disable 	*/
```

---

## Nested Disabling

Disable directives can be nested.

```sql
/* tsqlrefine-disable */
SELECT * FROM t1;           -- Suppressed by outer disable

/* tsqlrefine-disable */
SELECT * FROM t2;           -- Suppressed by both disables
/* tsqlrefine-enable */

SELECT * FROM t3;           -- Still suppressed by outer disable
/* tsqlrefine-enable */

SELECT * FROM t4;           -- Not suppressed
```

The inner `enable` closes the inner `disable`, and the outer `enable` closes the outer `disable`.

---

## Notes

### Line-Based Suppression

Disabling is applied on a line basis. If a diagnostic's start line is within the disabled range, that diagnostic is suppressed.

### Parse Errors Are Not Suppressed

Syntax errors (`parse-error`) are not affected by `tsqlrefine-disable`. This is because syntax errors indicate fundamental problems.

```sql
/* tsqlrefine-disable */
SELECT * FROM           -- Parse error is still reported
```

### Disable Range Start Position

Disable directives take effect from the line where the directive appears. Lines before the directive are not affected.

```sql
SELECT * FROM t1;       -- Not suppressed (before directive)
/* tsqlrefine-disable */
SELECT * FROM t2;       -- Suppressed
```

### Missing Corresponding Enable

If there is no `tsqlrefine-enable`, disabling continues until the end of the file.

---

## Sample Files

Usage examples are available in the `samples/sql/inline-disable/` directory:

- `disable-all.sql` - Disable all rules
- `disable-specific.sql` - Disable specific rules
- `disable-region.sql` - Disable only specific regions
- `disable-multiple.sql` - Disable multiple rules
- `disable-with-reason.sql` - Disable rules with reason text

---

## Checking Rule IDs

Available rule IDs can be checked with the `list-rules` command:

```bash
tsqlrefine list-rules
```

Example output:

```
avoid-select-star       Performance     Warning     fixable=False
dml-without-where       Safety          Error       fixable=False
...
```
