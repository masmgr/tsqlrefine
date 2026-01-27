# Avoid EXEC Dynamic SQL

**Rule ID:** `avoid-exec-dynamic-sql`
**Category:** Security
**Severity:** Warning
**Fixable:** No

## Description

Detects `EXEC` with dynamic SQL (using variables or string literals) which can be vulnerable to SQL injection attacks.

## Rationale

Dynamic SQL executed with `EXEC(@variable)` or `EXEC('string')` poses serious security risks:

- **SQL Injection vulnerability**: If the SQL string contains user input, attackers can inject malicious SQL commands
- **Privilege escalation**: Injected SQL runs with the permissions of the executing context
- **Data breach risk**: Attackers can read, modify, or delete any data accessible to the application
- **Difficult to audit**: Dynamic SQL is harder to review for security vulnerabilities
- **Performance issues**: Cannot benefit from query plan caching like parameterized queries

**Safer alternatives:**
- Use `sp_executesql` with parameters to prevent SQL injection
- Use static stored procedures instead of dynamic SQL
- If dynamic SQL is absolutely necessary, rigorously validate all inputs

## Examples

### Bad

```sql
-- Variable execution - vulnerable to SQL injection
EXEC(@sql);

-- String literal execution
EXEC('SELECT * FROM users');

-- Concatenated variables - highly vulnerable
EXEC(@part1 + @part2);

-- Long form - still vulnerable
EXECUTE(@dynamicQuery);
```

### Good

```sql
-- Static stored procedure - no SQL injection risk
EXEC dbo.GetUsers;

-- Stored procedure with parameters
EXEC MyStoredProc @id = 1, @name = 'test';

-- sp_executesql with parameters (safe dynamic SQL)
EXECUTE sp_executesql
    N'SELECT * FROM users WHERE id = @id',
    N'@id INT',
    @id = @userId;

-- Static procedure call with parentheses
EXEC dbo.GetUsers();

-- sp_executesql with multiple parameters
DECLARE @sql NVARCHAR(MAX) = N'SELECT * FROM users WHERE name = @name AND age > @age';
EXEC sp_executesql @sql,
    N'@name NVARCHAR(50), @age INT',
    @name = @userName,
    @age = 18;
```

## Configuration

This rule can be disabled in your ruleset configuration:

```json
{
  "rules": [
    { "id": "avoid-exec-dynamic-sql", "enabled": false }
  ]
}
```

## Important Notes

- If dynamic SQL is unavoidable, **always** use `sp_executesql` with parameterized queries
- Never concatenate user input directly into SQL strings
- Consider using ORM frameworks or query builders that handle parameterization automatically
- Validate and sanitize all inputs, even when using parameters

## See Also

- Related to SQL injection prevention and secure coding practices
