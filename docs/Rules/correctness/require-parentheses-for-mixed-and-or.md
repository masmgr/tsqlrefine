# Require Parentheses For Mixed And Or

**Rule ID:** `require-parentheses-for-mixed-and-or`
**Category:** Correctness
**Severity:** Warning
**Fixable:** No

## Description

Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion.

## Rationale

Mixed AND/OR operators without parentheses cause **precedence confusion** leading to incorrect query results.

**Operator precedence rules** (often forgotten):

SQL evaluates operators in this order:
1. **Parentheses** `()`
2. **NOT**
3. **AND** ← Higher precedence
4. **OR** ← Lower precedence

**The problem**: Humans often misread mixed AND/OR expressions.

```sql
-- What developers THINK this means:
WHERE active = 1 AND status = 'ok' OR role = 'admin'
-- "Give me users who are (active AND ok-status) OR admins"

-- What SQL Server ACTUALLY evaluates (AND binds tighter):
WHERE active = 1 AND (status = 'ok' OR role = 'admin')
-- "Give me active users who are EITHER ok-status OR admins"
```

**Real-world bug example**:

```sql
-- Intended: "Show paid orders from VIP customers, OR any pending orders"
SELECT * FROM Orders
WHERE CustomerType = 'VIP' AND Status = 'Paid' OR Status = 'Pending';

-- Developer thinks: (VIP AND Paid) OR Pending
-- Expected: VIP paid orders + all pending orders

-- SQL Server evaluates: VIP AND (Paid OR Pending)
-- Actual result: Only VIP orders that are paid OR pending
-- BUG: Missing non-VIP pending orders!
```

**Different interpretations, different results**:

```sql
-- Original query (ambiguous)
WHERE Active = 1 AND Role = 'Manager' OR Department = 'Sales'

-- Interpretation 1 (what most people think):
WHERE (Active = 1 AND Role = 'Manager') OR Department = 'Sales'
-- Result: Active managers + all sales department employees

-- Interpretation 2 (what SQL Server does):
WHERE Active = 1 AND (Role = 'Manager' OR Department = 'Sales')
-- Result: Active employees who are EITHER managers OR in sales
-- BUG: Missing inactive sales employees!
```

**Why this causes bugs**:

1. **Precedence is non-obvious**: Most people don't remember AND > OR
2. **Different from natural language**: English "and/or" doesn't have precedence
3. **Code review failures**: Reviewers misread intent
4. **Maintenance errors**: Future developers misunderstand logic
5. **Testing gaps**: Bug only appears with specific data combinations

**Security implications**:

```sql
-- Intended: "Allow access if (admin) OR (manager AND approved)"
WHERE Role = 'Admin' OR Role = 'Manager' AND Approved = 1

-- What SQL does: (Admin OR Manager) AND Approved = 1
-- Result: Admins ALSO need Approved = 1 (security bug!)

-- Correct with parentheses:
WHERE Role = 'Admin' OR (Role = 'Manager' AND Approved = 1)
-- Result: Admins always allowed, managers only if approved
```

**Performance implications**:

```sql
-- Without parentheses (forces table scan)
WHERE Status = 'Active' OR UserId = @UserId AND CreatedDate > @Date

-- SQL evaluates as: Status = 'Active' OR (UserId = @UserId AND CreatedDate > @Date)
-- Cannot use index on UserId efficiently

-- With parentheses (can use index)
WHERE (Status = 'Active' OR UserId = @UserId) AND CreatedDate > @Date
-- Can use index on (CreatedDate, Status, UserId)
```

**When parentheses are NOT needed**:

1. **Single operator type**: `WHERE A AND B AND C` (all AND, no confusion)
2. **Single operator type**: `WHERE A OR B OR C` (all OR, no confusion)
3. **Already parenthesized**: `WHERE (A AND B) OR C` (explicit grouping)

**Best practice**: Always use parentheses when mixing AND/OR, even if you know the precedence rules. Code should be obvious to all readers, not just experts.

## Examples

### Bad

```sql
-- Mixed AND/OR without parentheses (ambiguous)
SELECT * FROM Users WHERE Active = 1 AND Status = 'ok' OR Role = 'admin';

-- Complex mixed conditions (very confusing)
SELECT * FROM Orders
WHERE CustomerId = 100 AND Status = 'Paid' OR Status = 'Pending' AND ShippedDate IS NULL;

-- Security check without parentheses (dangerous)
SELECT * FROM Documents
WHERE OwnerId = @UserId OR IsPublic = 1 AND Approved = 1;

-- Multiple mixed operators (unreadable)
SELECT * FROM Products
WHERE CategoryId = 1 AND InStock = 1 OR CategoryId = 2 AND Price < 100 OR Featured = 1;

-- JOIN with mixed AND/OR (confusing)
SELECT * FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId
WHERE o.Status = 'Active' AND c.VIP = 1 OR o.Total > 1000;

-- UPDATE with mixed conditions (risky)
UPDATE Users
SET Locked = 1
WHERE FailedLogins > 5 AND LastLogin < DATEADD(DAY, -30, GETDATE()) OR Suspicious = 1;

-- DELETE with mixed conditions (dangerous!)
DELETE FROM Sessions
WHERE UserId = @UserId AND Expired = 1 OR CreatedDate < DATEADD(HOUR, -24, GETDATE());
```

### Good

```sql
-- Explicit parentheses (clear intent)
SELECT * FROM Users WHERE (Active = 1 AND Status = 'ok') OR Role = 'admin';

-- Alternative grouping (different logic, but explicit)
SELECT * FROM Users WHERE Active = 1 AND (Status = 'ok' OR Role = 'admin');

-- Complex conditions with parentheses (readable)
SELECT * FROM Orders
WHERE (CustomerId = 100 AND Status = 'Paid') OR (Status = 'Pending' AND ShippedDate IS NULL);

-- Security check with explicit grouping (safe)
SELECT * FROM Documents
WHERE OwnerId = @UserId OR (IsPublic = 1 AND Approved = 1);

-- Multiple conditions with clear grouping (maintainable)
SELECT * FROM Products
WHERE (CategoryId = 1 AND InStock = 1)
   OR (CategoryId = 2 AND Price < 100)
   OR Featured = 1;

-- JOIN with explicit parentheses
SELECT * FROM Orders o
JOIN Customers c ON o.CustomerId = c.CustomerId
WHERE (o.Status = 'Active' AND c.VIP = 1) OR o.Total > 1000;

-- UPDATE with clear conditions (safe)
UPDATE Users
SET Locked = 1
WHERE (FailedLogins > 5 AND LastLogin < DATEADD(DAY, -30, GETDATE())) OR Suspicious = 1;

-- DELETE with explicit grouping (safer)
DELETE FROM Sessions
WHERE (UserId = @UserId AND Expired = 1) OR CreatedDate < DATEADD(HOUR, -24, GETDATE());

-- Multiple levels of nesting (very explicit)
SELECT * FROM Employees
WHERE (Department = 'Sales' AND (Active = 1 OR OnLeave = 1))
   OR (Department = 'Support' AND Shift = 'Night');

-- All AND (no parentheses needed, no ambiguity)
SELECT * FROM Orders WHERE Status = 'Paid' AND ShippedDate IS NOT NULL AND Total > 100;

-- All OR (no parentheses needed, no ambiguity)
SELECT * FROM Products WHERE CategoryId = 1 OR CategoryId = 2 OR CategoryId = 3;

-- Showing different meanings with same operators
-- Meaning 1: (Active managers) OR (all sales employees)
SELECT * FROM Employees
WHERE (Active = 1 AND Role = 'Manager') OR Department = 'Sales';

-- Meaning 2: Active employees who are (managers OR sales)
SELECT * FROM Employees
WHERE Active = 1 AND (Role = 'Manager' OR Department = 'Sales');

-- NOT operator with parentheses (clear precedence)
SELECT * FROM Users
WHERE NOT (Deleted = 1 OR Suspended = 1) AND Active = 1;
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
    { "id": "require-parentheses-for-mixed-and-or", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
