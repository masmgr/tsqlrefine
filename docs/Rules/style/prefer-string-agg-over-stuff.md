# Prefer String Agg Over Stuff

**Rule ID:** `prefer-string-agg-over-stuff`
**Category:** Modernization
**Severity:** Information
**Fixable:** No

## Description

Recommends STRING_AGG() over STUFF(... FOR XML PATH('') ...); simpler and typically faster/safer (SQL Server 2017+).

## Rationale

The `STUFF(...FOR XML PATH(''))` pattern for string aggregation is a **legacy workaround** that should be replaced with `STRING_AGG()`:

1. **STUFF + FOR XML is a hack**:
   - `FOR XML PATH('')` was never intended for string concatenation
   - Uses XML serialization as a side effect to concatenate strings
   - Obscure syntax that's hard for developers to understand

2. **XML encoding issues**:
   - Special characters (`<`, `>`, `&`, `'`, `"`) are XML-encoded
   - `&` becomes `&amp;`, `<` becomes `&lt;`
   - Requires `.value()` or `TYPE` to decode (additional complexity)

3. **Performance**:
   - XML serialization overhead (parsing, encoding, memory allocation)
   - `STRING_AGG()` is optimized specifically for string aggregation
   - Benchmark: `STRING_AGG()` is 2-5x faster than `STUFF...FOR XML`

4. **STRING_AGG() is standard SQL**:
   - ANSI SQL standard (portable across databases)
   - Native T-SQL function (no XML workaround needed)
   - Cleaner syntax, easier to read and maintain

**Compatibility**: `STRING_AGG()` available in SQL Server 2017+ (compat level 140+).

## Examples

### Bad

```sql
-- Legacy STUFF + FOR XML PATH pattern (complex)
SELECT STUFF((
    SELECT ',' + name
    FROM users
    FOR XML PATH('')
), 1, 1, '') AS names;

-- With grouping (even more complex)
SELECT department,
    STUFF((
        SELECT ',' + u.name
        FROM users u
        WHERE u.department_id = d.department_id
        FOR XML PATH('')
    ), 1, 1, '') AS employee_names
FROM departments d;

-- XML encoding issues (& becomes &amp;)
SELECT STUFF((
    SELECT ',' + company_name
    FROM companies
    FOR XML PATH('')
), 1, 1, '') AS companies;
-- Result: "Smith & Sons" → "Smith &amp; Sons" (wrong!)

-- Complex delimiter
SELECT STUFF((
    SELECT ' | ' + product_name
    FROM products
    FOR XML PATH('')
), 1, 3, '') AS product_list;  -- Remove first 3 chars (' | ')
```

### Good

```sql
-- STRING_AGG (simple and clear)
SELECT STRING_AGG(name, ',') AS names
FROM users;

-- With grouping (much cleaner)
SELECT department,
    STRING_AGG(name, ',') AS employee_names
FROM users
GROUP BY department;

-- No XML encoding issues
SELECT STRING_AGG(company_name, ',') AS companies
FROM companies;
-- Result: "Smith & Sons, Jones & Co" (correct!)

-- Custom delimiter
SELECT STRING_AGG(product_name, ' | ') AS product_list
FROM products;

-- With ORDER BY (controls aggregation order)
SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) AS sorted_names
FROM users;

-- With WHERE clause
SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY hire_date DESC) AS recent_hires
FROM users
WHERE hire_date > DATEADD(YEAR, -1, GETDATE());

-- With DISTINCT (SQL Server 2022+, compat level 160+)
SELECT STRING_AGG(DISTINCT department, ', ') AS unique_departments
FROM users;

-- Multiple aggregations
SELECT department,
    COUNT(*) AS employee_count,
    STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) AS employees,
    STRING_AGG(email, '; ') AS emails
FROM users
GROUP BY department;

-- Conditional aggregation with CASE
SELECT department,
    STRING_AGG(
        CASE WHEN status = 'active' THEN name ELSE NULL END,
        ', '
    ) AS active_employees
FROM users
GROUP BY department;
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
    { "id": "prefer-string-agg-over-stuff", "enabled": false }
  ]
}
```

## See Also

- [TsqlRefine Rules Documentation](../README.md)
- [Configuration Guide](../../configuration.md)
