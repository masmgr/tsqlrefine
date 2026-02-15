# Avoid OPENROWSET / OPENDATASOURCE

**Rule ID:** `avoid-openrowset-opendatasource`
**Category:** Security
**Severity:** Warning
**Fixable:** No

## Description

Detects OPENROWSET and OPENDATASOURCE usage, which can be exploited for unauthorized remote data access.

## Rationale

`OPENROWSET` and `OPENDATASOURCE` allow ad-hoc connections to remote data sources directly from T-SQL. While useful for one-off data access, they pose significant security risks:

- **SQL injection attack surface**: Connection strings and queries passed to these functions can be manipulated if constructed dynamically
- **Data exfiltration**: Attackers can use these functions to send data to external servers
- **Unauthorized file access**: `OPENROWSET(BULK ...)` can read files from the server's file system
- **Credential exposure**: Connection strings may contain embedded credentials

Use linked servers with proper security configuration, ETL processes (SSIS), or application-level data access instead.

## Examples

### Bad

```sql
-- Remote data access via OPENROWSET
SELECT *
FROM OPENROWSET('SQLNCLI', 'Server=remote;Trusted_Connection=yes;',
    'SELECT * FROM dbo.Users') AS t;

-- File access via OPENROWSET BULK
SELECT *
FROM OPENROWSET(BULK 'C:\data\file.csv',
    FORMATFILE = 'C:\data\format.fmt') AS t;

-- Ad-hoc remote connection via OPENDATASOURCE
SELECT *
FROM OPENDATASOURCE('SQLNCLI',
    'Data Source=remote;Integrated Security=SSPI')
    .AdventureWorks.dbo.Users;
```

### Good

```sql
-- Use proper table references
SELECT * FROM dbo.Users;

-- Use OPENJSON for JSON data
SELECT * FROM OPENJSON(@json) AS j;

-- Use linked servers with proper security
SELECT * FROM LinkedServer.Database.dbo.Users;
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-openrowset-opendatasource", "enabled": false }
  ]
}
```

## See Also

- [avoid-linked-server](../performance/avoid-linked-server.md) - Detects linked server queries (4-part identifiers)
- [avoid-exec-dynamic-sql](avoid-exec-dynamic-sql.md) - Detects dynamic SQL injection risks
- [TsqlRefine Rules Documentation](../README.md)
