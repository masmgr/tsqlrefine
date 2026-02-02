# Dml Without Where

**Rule ID:** `dml-without-where`
**Category:** Safety
**Severity:** Error
**Fixable:** No

## Description

Detects UPDATE/DELETE statements without WHERE clause to prevent catastrophic unintended mass data modifications.

## Rationale

UPDATE/DELETE without WHERE clause affects **ALL rows in the table**, causing potentially catastrophic data loss.

**Business impact of accidental mass operations**:

1. **Catastrophic data loss**: Entire tables can be wiped or corrupted in milliseconds
2. **Customer impact**: Loss of customer data, order history, user accounts
3. **Financial records**: Deletion of financial transactions, invoices, payment records
4. **Audit trails**: Loss of compliance data, audit logs, change history
5. **Recovery costs**:
   - Restore from backup may lose hours or days of work
   - Downtime during recovery
   - Manual data reconciliation
   - Customer communication and reputation damage
6. **Compliance violations**: May breach GDPR, HIPAA, SOX, PCI-DSS regulations

**Common scenarios that cause this**:

- **Development mistakes**: Forgot WHERE clause while testing in production
- **Copy-paste errors**: Incomplete query copied from another context
- **Wrong connection**: Accidentally executed on production instead of dev/test database
- **Context confusion**: Wrong database selected in query tool
- **Incomplete refactoring**: Removed WHERE clause during code changes

**Real-world example**:

```sql
-- Developer meant to deactivate one test user
DELETE FROM Users;  -- Accidentally deleted ALL users (millions of records)
```

**If you truly need to modify all rows**:

1. **Add explicit WHERE 1=1** to signal intent:
   ```sql
   UPDATE Users SET Active = 1 WHERE 1=1;  -- Clearly intentional
   ```

2. **Use TRUNCATE TABLE for deletions** (faster, better logging):
   ```sql
   TRUNCATE TABLE TempData;  -- Explicit table-wide operation
   ```

3. **Document the reason in comments**:
   ```sql
   -- Business requirement: Reset all user passwords after security breach
   UPDATE Users SET PasswordHash = NULL, MustChangePassword = 1 WHERE 1=1;
   ```

4. **Use transactions with verification**:
   ```sql
   BEGIN TRANSACTION;
       UPDATE Users SET Active = 0 WHERE 1=1;

       -- Verify count matches expectation
       IF @@ROWCOUNT <> (SELECT COUNT(*) FROM Users)
       BEGIN
           ROLLBACK;
           RAISERROR('Unexpected row count', 16, 1);
           RETURN;
       END
   COMMIT;
   ```

## Exceptions

This rule does **not** flag UPDATE/DELETE without WHERE on:

- **Local temporary tables** (`#temp`) - Session-scoped, automatically dropped
- **Global temporary tables** (`##globaltemp`) - Temporary by nature
- **Table variables** (`@tablevar`) - Batch-scoped, no persistence risk
- **Alias-targeted DML** - When the target uses an alias from the FROM clause
- **INNER JOIN present** - When the FROM clause contains an INNER JOIN

### Temporary Objects

These temporary objects don't pose the same data safety risk as permanent tables since they are automatically cleaned up and contain only transient data.

```sql
-- These are allowed (no violation)
DELETE FROM #tempResults;
UPDATE ##globalCache SET processed = 1;
DELETE FROM @batchItems;
```

### Alias-Targeted DML

When a DML statement targets an alias defined in the FROM clause, it indicates more complex, intentional logic. The developer is explicitly thinking about table relationships.

```sql
-- These are allowed (alias target)
UPDATE u SET active = 1 FROM users u;
DELETE u FROM users u;
UPDATE u SET u.name = o.name FROM users u INNER JOIN other_users o ON u.id = o.id;
```

### INNER JOIN Present

When an INNER JOIN is present, it inherently filters rows (only matching rows from both tables are affected). This reduces the risk of unintended mass modifications.

```sql
-- These are allowed (INNER JOIN provides filtering)
UPDATE users SET active = 1 FROM users INNER JOIN orders ON users.id = orders.user_id;
DELETE FROM users FROM users INNER JOIN inactive_list ON users.id = inactive_list.user_id;
```

**Note**: LEFT/RIGHT/FULL OUTER JOINs and CROSS JOINs do **not** exempt the rule because they don't guarantee row filtering.

## Examples

### Bad

```sql
-- Accidentally deletes ALL users
DELETE FROM Users;

-- Updates ALL orders (likely unintended)
UPDATE Orders SET Status = 'Cancelled';

-- Wipes all financial records
DELETE FROM Transactions;

-- Common mistake: forgot to add WHERE clause
UPDATE Products SET Price = Price * 1.1;  -- Increases ALL product prices
```

### Good

```sql
-- Deletes specific user
DELETE FROM Users WHERE UserId = 123;

-- Updates orders for specific customer
UPDATE Orders SET Status = 'Cancelled' WHERE CustomerId = 456;

-- Deletes old transactions
DELETE FROM Transactions WHERE TransactionDate < DATEADD(YEAR, -7, GETDATE());

-- Intentional mass update (explicitly marked)
UPDATE Products SET Price = Price * 1.1 WHERE 1=1;  -- Holiday sale: all products

-- Better: Use TRUNCATE for table-wide deletion
TRUNCATE TABLE TempImportData;  -- Explicit and faster
```

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
    { "id": "dml-without-where", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
