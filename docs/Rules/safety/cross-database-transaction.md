# Cross Database Transaction

**Rule ID:** `cross-database-transaction`
**Category:** Safety
**Severity:** Warning
**Fixable:** No

## Description

Discourages cross-database transactions to avoid distributed transaction complexity, deadlocks, and availability issues.

## Rationale

Cross-database transactions introduce significant risks that are difficult to diagnose and recover from.

**Risks of cross-database transactions**:

1. **Distributed transaction escalation**: May escalate to MS DTC (Distributed Transaction Coordinator), causing performance degradation
2. **Deadlock complexity**: Cross-database locks are harder to diagnose and prevent
3. **Recovery challenges**: Restore operations become complex with cross-database dependencies
4. **Availability**: One database offline affects all dependent databases
5. **Performance**: Network round-trips and coordination overhead

**When does this occur?**

- Explicit BEGIN TRANSACTION with operations on multiple databases
- Triggers that modify other databases within transactions
- Linked server queries within transactions

**Alternatives**:

- Consolidate related tables into single database
- Use Service Broker for asynchronous cross-database operations
- Application-level coordination instead of database transactions
- Message queue pattern (outbox pattern) for cross-database consistency

## Examples

### Bad

```sql
-- Cross-database transaction (risky)
BEGIN TRANSACTION;
    UPDATE DB1.dbo.Customers SET Status = 'Active' WHERE CustomerId = 123;
    UPDATE DB2.dbo.Orders SET Processed = 1 WHERE CustomerId = 123;  -- Different database!
COMMIT;

-- Trigger causing cross-database transaction
CREATE TRIGGER trg_UpdateLog ON DB1.dbo.Customers
AFTER UPDATE AS
BEGIN
    INSERT INTO DB2.dbo.AuditLog (Message)  -- Cross-database operation!
    VALUES ('Customer updated');
END;

-- Linked server in transaction
BEGIN TRANSACTION;
    UPDATE LocalDB.dbo.Orders SET Status = 'Shipped';
    UPDATE LinkedServer.RemoteDB.dbo.ShipmentLog SET Processed = 1;  -- Distributed transaction!
COMMIT;
```

### Good

```sql
-- Single database transaction (safe)
BEGIN TRANSACTION;
    UPDATE Customers SET Status = 'Active' WHERE CustomerId = 123;
    UPDATE Orders SET Processed = 1 WHERE CustomerId = 123;  -- Same database
COMMIT;

-- Alternative: Use message queue for cross-database operations (outbox pattern)
BEGIN TRANSACTION;
    UPDATE Customers SET Status = 'Active' WHERE CustomerId = 123;
    -- Queue cross-database operation for async processing
    INSERT INTO OutboxQueue (TargetDB, Operation, Payload)
    VALUES ('DB2', 'UpdateOrders', '{"CustomerId": 123, "Processed": 1}');
COMMIT;
-- Separate process handles OutboxQueue asynchronously

-- Application-level coordination (two separate transactions)
-- Transaction 1
BEGIN TRANSACTION;
    UPDATE DB1.dbo.Customers SET Status = 'Active' WHERE CustomerId = 123;
COMMIT;

-- Transaction 2 (application coordinates, not DB transaction)
BEGIN TRANSACTION;
    UPDATE DB2.dbo.Orders SET Processed = 1 WHERE CustomerId = 123;
COMMIT;
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
    { "id": "cross-database-transaction", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
