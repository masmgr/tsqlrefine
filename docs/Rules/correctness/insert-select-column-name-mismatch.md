# insert-select-column-name-mismatch

## Summary

Warns when column names in an `INSERT ... SELECT` statement do not match between the INSERT target list and the SELECT output list.
This heuristic helps detect accidental column order mismatches that can silently corrupt data.

---

## Description

When using `INSERT INTO <table> (col1, col2, ...) SELECT ...`, SQL Server maps values by position, not by column name.

This rule compares:

- The column names listed in the INSERT target clause, and
- The resolved output column names from the SELECT list

If column names appear mismatched by position, the rule emits an Information-level hint, indicating a potential accidental column swap.

Because this is a heuristic and not all mismatches are errors, the rule is intentionally non-blocking.

---

## Why this matters

Column order mismatches in `INSERT ... SELECT` can:

- Pass validation successfully
- Produce incorrect data
- Be difficult to detect in code review
- Cause silent data corruption

This rule provides a low-noise safeguard against such mistakes.

---

## Examples

### Triggers a hint (columns appear swapped)

```sql
INSERT INTO Users (Id, Name)
SELECT Name, Id
FROM Users_Staging;
```

**Message:**

INSERT target column names do not match SELECT output column names. This may indicate a swapped column order.

---

### Triggers a hint (partially mismatched names)

```sql
INSERT INTO Orders (OrderDate, ShipDate)
SELECT ShipDate, OrderDate
FROM ImportedOrders;
```

---

### No hint (column names aligned)

```sql
INSERT INTO Users (Id, Name)
SELECT Id, Name
FROM Users_Staging;
```

---

### Skipped (expressions or functions in SELECT)

```sql
INSERT INTO Logs (CreatedAt, Message)
SELECT GETDATE(), Message
FROM TempLogs;
```

This rule does not attempt to infer intent when SELECT output columns are expressions, literals, or complex computations.

---

### Skipped (column count mismatch handled by another rule)

```sql
INSERT INTO Users (Id, Name)
SELECT Id
FROM Users_Staging;
```

Handled by: `semantic/insert-column-count-mismatch`

---

## Detection Logic (Heuristic)

1. Applies only to `INSERT INTO <table> (column_list) SELECT ...` patterns
2. Extracts ordered INSERT target column names
3. Extracts resolved SELECT output names:
   - Uses alias if present (`AS Alias`)
   - Otherwise uses column identifier if resolvable
4. Skips SELECT expressions that are:
   - Functions
   - Literals
   - Complex expressions
5. Compares names positionally
6. Emits a hint if names differ in corresponding positions

---

## False Positives and Limitations

This rule does not guarantee an actual bug.
Intentional remapping or staging transformations may trigger it.

Examples of legitimate mismatches:

- ETL transformations
- Column renaming during migration
- Compatibility shims for legacy schemas

Therefore:

- Severity is Information
- The rule is advisory only

---

## Category

Correctness

---

## Severity

Information

---

## Fixable

No

---

## Related Rules

- `require-column-list-for-insert-select`
- `semantic/insert-column-count-mismatch`
- `avoid-select-star`

---

## Recommended Usage

This rule is particularly useful in:

- ETL pipelines
- Data import scripts
- Migration tooling
- Staging to production table loads

---

## Optional Enhancements (Future)

- Similarity scoring to detect partial matches
- Configurable minimum match ratio
- Optional stricter mode for `strict` preset
