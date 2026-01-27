# Avoid @@IDENTITY

**Rule ID:** `avoid-atat-identity`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Disallows the use of `@@IDENTITY` because it can return values from triggers, leading to incorrect identity values being retrieved.

## Rationale

The `@@IDENTITY` function has a critical flaw: it returns the last identity value generated in the current session, **regardless of scope**. This means:

- **Trigger interference**: If a trigger on the inserted table also inserts into another table with an identity column, `@@IDENTITY` returns the identity from the trigger's insert, not your original insert
- **Unexpected results**: You might retrieve an identity value from a completely different table than intended
- **Silent bugs**: The query may appear to work correctly until a trigger is added, then suddenly start returning wrong values
- **Difficult debugging**: The root cause (trigger inserting into another table) may not be obvious

**Better alternatives:**
- `SCOPE_IDENTITY()`: Returns the last identity value inserted in the current scope (ignores triggers)
- `OUTPUT` clause: Returns identity values directly from the INSERT statement
- `IDENT_CURRENT('table_name')`: Returns the last identity value for a specific table

## Examples

### Bad

```sql
-- @@IDENTITY can return wrong value if trigger inserts into other tables
INSERT INTO users (name) VALUES ('John');
SELECT @@IDENTITY;  -- May return ID from trigger's insert!

-- Case-insensitive variant - still problematic
SELECT @@identity;
```

### Good

```sql
-- SCOPE_IDENTITY() ignores triggers and returns only your insert's ID
INSERT INTO users (name) VALUES ('John');
SELECT SCOPE_IDENTITY();

-- OUTPUT clause returns identity directly from INSERT
INSERT INTO users (name)
OUTPUT INSERTED.id
VALUES ('John');

-- For specific table's last identity (any session)
SELECT IDENT_CURRENT('users');

-- Store identity in variable using OUTPUT
DECLARE @NewId INT;
INSERT INTO users (name)
OUTPUT INSERTED.id INTO @NewId
VALUES ('John');
SELECT @NewId;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-atat-identity", "enabled": false }
  ]
}
```

## See Also

- Related to proper identity retrieval patterns
