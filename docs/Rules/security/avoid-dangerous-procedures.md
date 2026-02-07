# Avoid Dangerous Procedures

**Rule ID:** `avoid-dangerous-procedures`
**Category:** Security
**Severity:** Warning
**Fixable:** No

## Description

Detects usage of dangerous extended stored procedures (xp_cmdshell, xp_reg*, sp_OA*) that pose security risks.

## Rationale

Extended stored procedures like `xp_cmdshell`, `xp_reg*`, and `sp_OA*` provide low-level access to the operating system, Windows registry, and COM objects. These capabilities can be exploited by attackers to:

- **xp_cmdshell**: Execute arbitrary OS commands, enabling remote code execution
- **xp_regread/xp_regwrite**: Read and modify Windows registry keys, potentially altering system configuration
- **sp_OACreate/sp_OAMethod**: Create and invoke COM objects, enabling file system access and other dangerous operations

These procedures should be avoided in application code. If OS-level operations are necessary, use safer alternatives such as CLR integration, SSIS packages, or dedicated application services.

## Detected Procedures

| Procedure | Risk |
|-----------|------|
| `xp_cmdshell` | OS command execution |
| `xp_regread` | Registry read access |
| `xp_regwrite` | Registry write access |
| `xp_regdeletekey` | Registry key deletion |
| `xp_regdeletevalue` | Registry value deletion |
| `xp_regaddmultistring` | Registry multi-string manipulation |
| `xp_regremovemultistring` | Registry multi-string manipulation |
| `sp_OACreate` | COM object creation |
| `sp_OAMethod` | COM method invocation |
| `sp_OAGetProperty` | COM property read |
| `sp_OASetProperty` | COM property write |
| `sp_OADestroy` | COM object cleanup |
| `sp_OAGetErrorInfo` | COM error info retrieval |

## Examples

### Bad

```sql
-- OS command execution
EXEC xp_cmdshell 'dir C:\';

-- Registry manipulation
EXEC xp_regread @rootkey = N'HKEY_LOCAL_MACHINE',
    @key = N'SOFTWARE\Microsoft',
    @value_name = N'Version';

-- OLE Automation
DECLARE @obj INT;
EXEC sp_OACreate 'Scripting.FileSystemObject', @obj OUTPUT;
EXEC sp_OAMethod @obj, 'DeleteFile', NULL, 'C:\temp\file.txt';
EXEC sp_OADestroy @obj;
```

### Good

```sql
-- Use normal stored procedures
EXEC dbo.GetUsers @id = 1;

-- Use parameterized queries
EXEC sp_executesql N'SELECT @val', N'@val INT', @val = 1;

-- Use system stored procedures that are safe
EXEC sp_help 'dbo.Users';
```

## Configuration

To disable this rule:

```json
{
  "rules": [
    { "id": "avoid-dangerous-procedures", "enabled": false }
  ]
}
```

## See Also

- [avoid-exec-dynamic-sql](avoid-exec-dynamic-sql.md) - Detects dynamic SQL injection risks
- [TsqlRefine Rules Documentation](../README.md)
