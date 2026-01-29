# Set Transaction Isolation Level

**Rule ID:** `set-transaction-isolation-level`
**Category:** Configuration
**Severity:** Warning
**Fixable:** No

## Description

Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements to ensure predictable transaction behavior.

## Rationale

Explicit transaction isolation level setting ensures predictable concurrency behavior across different environments.

**Why explicit is better**:

1. **Default varies**: Server default can be changed (typically READ COMMITTED), causing unexpected behavior
2. **Clarity**: Readers understand concurrency requirements and locking strategy
3. **Prevents bugs**: Implicit defaults may cause issues in high-concurrency scenarios
4. **Reproducibility**: Explicit settings ensure same behavior across dev/test/prod

**Isolation levels** (performance vs. consistency trade-off):

| Level | Dirty Read | Non-Repeatable Read | Phantom Read | Performance | Use Case |
|-------|------------|---------------------|--------------|-------------|----------|
| READ UNCOMMITTED | Yes | Yes | Yes | Fastest (no locks) | Reporting, approximate counts |
| READ COMMITTED (default) | No | Yes | Yes | Good | Most OLTP operations |
| REPEATABLE READ | No | No | Yes | Slower (more locks) | Financial calculations |
| SERIALIZABLE | No | No | No | Slowest (range locks) | Critical consistency |
| SNAPSHOT | No | No | No | Good (row versioning) | High-concurrency reads |

**Concurrency phenomena**:

- **Dirty Read**: Reading uncommitted changes from other transactions
- **Non-Repeatable Read**: Same query returns different values within transaction (row modified)
- **Phantom Read**: Same query returns different rows within transaction (rows added/deleted)

## Examples

### Bad

```sql
-- No explicit isolation level (uses server default)
BEGIN TRANSACTION;
    SELECT COUNT(*) FROM Orders;  -- Uses server default (uncertain)
    INSERT INTO OrderLog (Message) VALUES ('Count completed');
COMMIT;

-- Implicit default may cause unexpected behavior
SELECT * FROM Orders WHERE Status = 'Pending';  -- May see uncommitted data if default is READ UNCOMMITTED
```

### Good

```sql
-- Explicit isolation level (READ COMMITTED for typical OLTP)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRANSACTION;
    SELECT COUNT(*) FROM Orders;  -- Known behavior: no dirty reads
    INSERT INTO OrderLog (Message) VALUES ('Count completed');
COMMIT;

-- SNAPSHOT for high-concurrency reads (no blocking)
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRANSACTION;
    SELECT * FROM Orders WHERE Status = 'Pending';  -- Consistent snapshot, no locks
    SELECT * FROM OrderDetails WHERE OrderId IN (SELECT OrderId FROM Orders WHERE Status = 'Pending');
COMMIT;

-- SERIALIZABLE for critical operations
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
    DECLARE @Balance DECIMAL(18,2);
    SELECT @Balance = Balance FROM Accounts WHERE AccountId = 123;  -- Locks range

    IF @Balance >= 100.00
    BEGIN
        UPDATE Accounts SET Balance = Balance - 100.00 WHERE AccountId = 123;
        INSERT INTO Transactions (AccountId, Amount) VALUES (123, -100.00);
    END
COMMIT;
```

**When to use each level**:

- **READ UNCOMMITTED**: Reporting queries where dirty reads acceptable, approximate aggregates
- **READ COMMITTED**: Default for most OLTP operations, balances consistency and performance
- **REPEATABLE READ**: Financial transactions, audit operations requiring stable reads
- **SERIALIZABLE**: Critical operations requiring absolute consistency (e.g., inventory allocation)
- **SNAPSHOT**: High-concurrency reads without blocking writers (requires database setting enabled)

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
    { "id": "set-transaction-isolation-level", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
