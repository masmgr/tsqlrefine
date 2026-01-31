# Require Ms Description For Table Definition File

**Rule ID:** `require-ms-description-for-table-definition-file`
**Category:** Schema Design
**Severity:** Information
**Fixable:** No

## Description

Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL.

## Rationale

**Extended properties** (specifically `MS_Description`) provide **self-documenting database schemas**:

1. **Documentation lives with the schema**:
   - Table purposes, column meanings, and business rules documented directly in the database
   - No separate documentation files that get out of sync
   - Viewable in SSMS Object Explorer (properties window)

2. **Improves discoverability**:
   - New developers can understand table purposes without asking
   - Database tools (SSMS, Azure Data Studio, schema comparison tools) display descriptions
   - Automated documentation generation (using SQL or third-party tools)

3. **Business context preservation**:
   - Why does this table exist? What business process does it support?
   - What is the meaning of cryptic column names or codes?
   - What are the validation rules or constraints?

4. **Code review and maintenance**:
   - Reviewers can verify table purpose matches implementation
   - Future refactoring is safer when intent is documented
   - Reduces "what does this table do?" questions

5. **Compliance and auditing**:
   - Some regulations require documented data schemas
   - Easier to produce data dictionaries for auditors

**Best practices:**
- Add `MS_Description` to tables, columns, procedures, functions
- Include business purpose, validation rules, and important notes
- Update descriptions when schema changes

## Examples

### Bad

```sql
-- No documentation (what is this table for?)
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100)
);

-- Table with cryptic name, no description
CREATE TABLE dbo.USR_STS_LKP (
    StatusId INT PRIMARY KEY,
    Code VARCHAR(10),
    DisplayName NVARCHAR(50)
);

-- Complex table with no business context
CREATE TABLE dbo.OrderTransactionLog (
    LogId BIGINT PRIMARY KEY,
    OrderId INT,
    ActionType VARCHAR(20),
    CreatedDate DATETIME,
    ProcessedBy INT,
    StatusFlag TINYINT
);
```

### Good

```sql
-- Table with description
CREATE TABLE dbo.Users (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100),
    Email NVARCHAR(255),
    CreatedDate DATETIME DEFAULT GETDATE()
);

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Stores user account information for the application. Each user must have a unique email address.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Users';

-- Column descriptions
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Unique identifier for the user account',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Users',
    @level2type = N'COLUMN', @level2name = N'Id';

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'User full name for display purposes',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Users',
    @level2type = N'COLUMN', @level2name = N'Name';

-- Lookup table with detailed description
CREATE TABLE dbo.USR_STS_LKP (
    StatusId INT PRIMARY KEY,
    Code VARCHAR(10) NOT NULL UNIQUE,
    DisplayName NVARCHAR(50) NOT NULL
);

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'User Status Lookup table. Maps status codes to display names. Valid codes: A (Active), I (Inactive), S (Suspended), D (Deleted).',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'USR_STS_LKP';

-- Audit/log table with business context
CREATE TABLE dbo.OrderTransactionLog (
    LogId BIGINT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    ActionType VARCHAR(20) NOT NULL,
    CreatedDate DATETIME DEFAULT GETDATE(),
    ProcessedBy INT,
    StatusFlag TINYINT DEFAULT 0
);

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Audit log for all order state transitions. Records user actions, system processes, and payment gateway callbacks. Retention: 7 years per compliance requirements.',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'OrderTransactionLog';

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Type of action performed. Valid values: CREATE, UPDATE, CANCEL, REFUND, PAYMENT_RECEIVED, SHIPPED',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'OrderTransactionLog',
    @level2type = N'COLUMN', @level2name = N'ActionType';

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Processing status flag: 0=Pending, 1=Processed, 2=Failed, 3=Retry',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'OrderTransactionLog',
    @level2type = N'COLUMN', @level2name = N'StatusFlag';

-- View descriptions with extended properties
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    ep.value AS TableDescription
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id
    AND ep.minor_id = 0
    AND ep.name = 'MS_Description'
WHERE s.name = 'dbo';
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
    { "id": "require-ms-description-for-table-definition-file", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
