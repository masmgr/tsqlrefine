# TsqlRefine Rules

This document provides a comprehensive overview of all built-in rules in TsqlRefine. TsqlRefine includes 83 rules covering various aspects of T-SQL code quality, from correctness and safety to performance and style.

## Table of Contents

- [Overview](#overview)
- [Rule Categories](#rule-categories)
- [Rules by Category](#rules-by-category)
- [Rules by Severity](#rules-by-severity)
- [Fixable Rules](#fixable-rules)
- [Individual Rule Documentation](#individual-rule-documentation)

## Overview

TsqlRefine provides a comprehensive set of rules to enforce T-SQL best practices and detect potential issues in SQL Server code. Each rule has:

- **Rule ID**: Unique identifier (e.g., `avoid-select-star`)
- **Description**: What the rule checks for
- **Category**: Classification of the rule type
- **Default Severity**: Error, Warning, or Information
- **Fixable**: Whether the rule supports auto-fixing

### Rule Statistics

- **Total Rules**: 83
- **Fixable Rules**: 8 (10%)
- **Error Severity**: 13 rules (16%)
- **Warning Severity**: 35 rules (42%)
- **Information Severity**: 35 rules (42%)

## Rule Categories

TsqlRefine organizes rules into the following categories:

| Category | Rules | Description |
|----------|-------|-------------|
| **Correctness** | 20 | Detects code that may produce incorrect results or runtime errors |
| **Safety** | 3 | Prevents destructive or dangerous operations |
| **Security** | 1 | Identifies security vulnerabilities like SQL injection |
| **Performance** | 16 | Flags patterns that can cause performance issues |
| **Naming** | 1 | Enforces naming conventions and readability |
| **Style** | 12 | Maintains code formatting and consistency |
| **Transactions** | 6 | Ensures proper transaction handling and session settings |
| **Schema** | 3 | Enforces database schema best practices |
| **Debug** | 1 | Controls debug and output statements |

## Rules by Category

### Correctness (20 rules)

Rules that detect code patterns that may produce incorrect results or runtime errors.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-nolock](correctness/avoid-nolock.md) | Avoid using NOLOCK hint or READ UNCOMMITTED isolation level | Warning | No |
| [require-column-list-for-insert-select](correctness/require-column-list-for-insert-select.md) | INSERT SELECT statements must explicitly specify the column list | Warning | No |
| [require-column-list-for-insert-values](correctness/require-column-list-for-insert-values.md) | INSERT VALUES statements must explicitly specify the column list | Warning | No |
| [avoid-null-comparison](correctness/avoid-null-comparison.md) | Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL | Warning | No |
| [require-parentheses-for-mixed-and-or](correctness/require-parentheses-for-mixed-and-or.md) | Detects mixed AND/OR operators without explicit parentheses | Warning | No |
| [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md) | Detects duplicate table aliases in the same scope | Error | No |
| [semantic/undefined-alias](correctness/semantic-undefined-alias.md) | Detects references to undefined table aliases in column qualifiers | Error | No |
| [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md) | Detects column count mismatches between target column list and source | Error | No |
| [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md) | Detects CTE name conflicts with other CTEs or table aliases | Error | No |
| [semantic/return-after-statements](correctness/semantic-return-after-statements.md) | Detects unreachable statements after a RETURN statement | Warning | No |
| [semantic/join-condition-always-true](correctness/semantic-join-condition-always-true.md) | Detects JOIN conditions that are always true or likely incorrect | Warning | No |
| [semantic/left-join-filtered-by-where](correctness/semantic-left-join-filtered-by-where.md) | Detects LEFT JOIN operations where WHERE clause filters the right-side table | Warning | No |
| [named-constraint](correctness/named-constraint.md) | Prohibit named constraints in temp tables to avoid naming conflicts | Error | No |
| [avoid-ambiguous-datetime-literal](correctness/avoid-ambiguous-datetime-literal.md) | Disallows slash-delimited date literals | Warning | No |
| [avoid-atat-identity](correctness/avoid-atat-identity.md) | Disallows @@IDENTITY; prefer SCOPE_IDENTITY() or OUTPUT | Warning | No |
| [order-by-in-subquery](correctness/order-by-in-subquery.md) | Disallows invalid ORDER BY in subqueries unless paired with TOP, OFFSET, FOR XML, or FOR JSON | Error | No |
| [escape-keyword-identifier](correctness/escape-keyword-identifier.md) | Warns when a T-SQL keyword is used as a table/column identifier without escaping | Warning | **Yes** |
| [semantic/alias-scope-violation](correctness/semantic-alias-scope-violation.md) | Detects potential scope violations where aliases from outer queries are referenced in inner queries | Warning | No |
| [semantic/unicode-string](correctness/semantic-unicode-string.md) | Detects Unicode characters in string literals assigned to non-Unicode variables | Error | **Yes** |
| [semantic/data-type-length](correctness/semantic-data-type-length.md) | Requires explicit length specification for variable-length data types | Error | No |

### Safety (3 rules)

Rules that prevent destructive or dangerous operations.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [dml-without-where](safety/dml-without-where.md) | Detects UPDATE/DELETE statements without WHERE clause | Error | No |
| [avoid-merge](safety/avoid-merge.md) | Avoid using MERGE statement due to known bugs and unpredictable behavior | Warning | No |
| [cross-database-transaction](safety/cross-database-transaction.md) | Discourage cross-database transactions | Warning | No |

### Security (1 rule)

Rules that identify security vulnerabilities.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-exec-dynamic-sql](security/avoid-exec-dynamic-sql.md) | Detects EXEC with dynamic SQL which can be vulnerable to SQL injection | Warning | No |

### Performance (16 rules)

Rules that flag patterns that can cause performance issues.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [top-without-order-by](performance/top-without-order-by.md) | Detects TOP clause without ORDER BY | Error | No |
| [avoid-implicit-conversion-in-predicate](performance/avoid-implicit-conversion-in-predicate.md) | Detects functions or conversions applied to columns in predicates | Warning | No |
| [disallow-cursors](performance/disallow-cursors.md) | Prohibit cursor usage; prefer set-based operations | Warning | No |
| [full-text](performance/full-text.md) | Prohibit full-text search predicates | Warning | No |
| [data-compression](performance/data-compression.md) | Recommend specifying DATA_COMPRESSION option in CREATE TABLE | Information | No |
| [information-schema](performance/information-schema.md) | Prohibit INFORMATION_SCHEMA views; use sys catalog views instead | Warning | No |
| [object-property](performance/object-property.md) | Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX instead | Warning | No |
| [linked-server](performance/linked-server.md) | Prohibit linked server queries (4-part identifiers) | Warning | No |
| [utc-datetime](performance/utc-datetime.md) | Detects local datetime functions and suggests UTC alternatives | Warning | No |
| [non-sargable](performance/non-sargable.md) | Detects functions applied to columns in predicates (excluding UPPER/LOWER/CAST/CONVERT) | Warning | No |
| [upper-lower](performance/upper-lower.md) | Detects UPPER or LOWER functions applied to columns in predicates | Warning | No |
| [avoid-select-star](performance/avoid-select-star.md) | Avoid SELECT * in queries | Warning | No |
| [avoid-top-in-dml](performance/avoid-top-in-dml.md) | Disallows TOP in UPDATE/DELETE | Warning | No |
| [disallow-select-into](performance/disallow-select-into.md) | Warns on SELECT ... INTO | Warning | No |
| [forbid-top-100-percent-order-by](performance/forbid-top-100-percent-order-by.md) | Forbids TOP 100 PERCENT ORDER BY | Warning | No |
| [avoid-heap-table](performance/avoid-heap-table.md) | Warns when tables are created as heaps (excludes temporary tables) | Warning | No |

### Naming (1 rule)

Rules that enforce naming conventions and readability.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [meaningful-alias](naming/meaningful-alias.md) | Use meaningful aliases instead of single-character aliases in multi-table queries | Information | No |

### Style (22 rules)

Rules that maintain code formatting and consistency.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [conditional-begin-end](style/conditional-begin-end.md) | Require BEGIN/END blocks in conditional statements | Information | No |
| [require-begin-end-for-if-with-controlflow-exception](style/require-begin-end-for-if-with-controlflow-exception.md) | Enforces BEGIN/END for IF/ELSE blocks | Warning | No |
| [require-begin-end-for-while](style/require-begin-end-for-while.md) | Enforces BEGIN/END for every WHILE body | Warning | No |
| [keyword-capitalization](style/keyword-capitalization.md) | SQL keywords should be in uppercase | Information | No |
| [duplicate-empty-line](style/duplicate-empty-line.md) | Avoid consecutive empty lines | Information | No |
| [nested-block-comments](style/nested-block-comments.md) | Avoid nested block comments | Warning | No |
| [join-keyword](style/join-keyword.md) | Detects comma-separated table lists in FROM clause | Warning | No |
| [count-star](style/count-star.md) | Detects COUNT(*) usage and suggests COUNT(1) or COUNT(column_name) | Information | No |
| [duplicate-go](style/duplicate-go.md) | Avoid consecutive GO batch separators | Information | No |
| [require-as-for-column-alias](style/require-as-for-column-alias.md) | Column aliases should use the AS keyword | Information | **Yes** |
| [require-as-for-table-alias](style/require-as-for-table-alias.md) | Table aliases should use the AS keyword | Information | **Yes** |
| [semicolon-termination](style/semicolon-termination.md) | SQL statements should be terminated with a semicolon | Information | **Yes** |
| [require-explicit-join-type](style/require-explicit-join-type.md) | Disallows ambiguous JOIN shorthand | Warning | **Yes** |
| [semantic/case-sensitive-variables](style/semantic-case-sensitive-variables.md) | Ensures variable references match the exact casing used in declarations | Warning | No |
| [qualified-select-columns](style/qualified-select-columns.md) | Requires qualifying columns in SELECT lists when multiple tables are referenced | Warning | No |
| [require-qualified-columns-everywhere](style/require-qualified-columns-everywhere.md) | Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced | Warning | No |
| [semantic/multi-table-alias](style/semantic-multi-table-alias.md) | Requires column references in multi-table queries to be qualified with table aliases | Warning | No |
| [semantic/schema-qualify](style/semantic-schema-qualify.md) | Requires all table references to include schema qualification | Warning | No |
| [prefer-coalesce-over-nested-isnull](style/prefer-coalesce-over-nested-isnull.md) | Detects nested ISNULL and recommends COALESCE | Information | No |
| [prefer-concat-over-plus-when-nullable-or-convert](style/prefer-concat-over-plus-when-nullable-or-convert.md) | Detects CAST/CONVERT in concatenations; stricter variant | Information | No |
| [prefer-concat-ws](style/prefer-concat-ws.md) | Recommends CONCAT_WS() when concatenation repeats the same separator | Information | No |
| [prefer-json-functions](style/prefer-json-functions.md) | Encourages built-in JSON features over manual string parsing | Information | No |
| [prefer-string-agg-over-stuff](style/prefer-string-agg-over-stuff.md) | Recommends STRING_AGG() over STUFF with FOR XML PATH | Information | No |
| [prefer-trim-over-ltrim-rtrim](style/prefer-trim-over-ltrim-rtrim.md) | Recommends TRIM() instead of LTRIM(RTRIM()) | Information | No |
| [prefer-try-convert-patterns](style/prefer-try-convert-patterns.md) | Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE | Information | No |
| [prefer-concat-over-plus](style/prefer-concat-over-plus.md) | Recommends CONCAT() when + concatenation uses ISNULL/COALESCE | Information | No |
| [avoid-magic-convert-style-for-datetime](style/avoid-magic-convert-style-for-datetime.md) | Warns on datetime CONVERT style numbers (magic numbers) | Information | No |

### Transactions (6 rules)

Rules that ensure proper transaction handling and session settings.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [require-try-catch-for-transaction](transactions/require-try-catch-for-transaction.md) | Requires TRY/CATCH around explicit transactions | Warning | No |
| [require-xact-abort-on](transactions/require-xact-abort-on.md) | Requires SET XACT_ABORT ON with explicit transactions | Warning | No |
| [set-ansi](transactions/set-ansi.md) | Files should start with SET ANSI_NULLS ON within the first 10 statements | Warning | No |
| [set-nocount](transactions/set-nocount.md) | Files should start with SET NOCOUNT ON within the first 10 statements | Warning | No |
| [set-quoted-identifier](transactions/set-quoted-identifier.md) | Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements | Warning | No |
| [set-transaction-isolation-level](transactions/set-transaction-isolation-level.md) | Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements | Warning | No |

### Schema (3 rules)

Rules that enforce database schema best practices.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [require-primary-key-or-unique-constraint](schema/require-primary-key-or-unique-constraint.md) | Requires PRIMARY KEY or UNIQUE constraints for user tables | Warning | No |
| [require-ms-description-for-table-definition-file](schema/require-ms-description-for-table-definition-file.md) | Ensures table definition files include MS_Description extended property | Information | No |
| [avoid-heap-table](schema/avoid-heap-table.md) | Warns when tables are created as heaps (excludes temporary tables) | Warning | No |

### Debug (1 rule)

Rules that control debug and output statements.

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [print-statement](debug/print-statement.md) | Prohibit PRINT statements; use RAISERROR instead | Information | No |

## Rules by Severity

### Error (13 rules)

Critical issues that should be fixed immediately.

- [dml-without-where](safety/dml-without-where.md)
- [top-without-order-by](performance/top-without-order-by.md)
- [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md)
- [semantic/undefined-alias](correctness/semantic-undefined-alias.md)
- [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md)
- [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md)
- [named-constraint](correctness/named-constraint.md)
- [order-by-in-subquery](correctness/order-by-in-subquery.md)
- [semantic/unicode-string](correctness/semantic-unicode-string.md)
- [semantic/data-type-length](correctness/semantic-data-type-length.md)
- [semantic/set-variable](correctness/semantic-set-variable.md)

### Warning (35 rules)

Issues that should be reviewed and potentially fixed.

See individual category sections above for the complete list.

### Information (35 rules)

Informational suggestions for code quality improvements.

See individual category sections above for the complete list.

## Fixable Rules

The following 8 rules support automatic fixing:

1. [escape-keyword-identifier](correctness/escape-keyword-identifier.md) - Automatically escapes SQL keywords used as identifiers
2. [require-as-for-column-alias](style/require-as-for-column-alias.md) - Adds AS keyword to column aliases
3. [require-as-for-table-alias](style/require-as-for-table-alias.md) - Adds AS keyword to table aliases
4. [require-explicit-join-type](query-structure/require-explicit-join-type.md) - Converts ambiguous JOIN to INNER JOIN
5. [semicolon-termination](style/semicolon-termination.md) - Adds semicolons to statement terminators
6. [semantic/unicode-string](correctness/semantic-unicode-string.md) - Adds N prefix to Unicode string literals

To apply auto-fixes, use the `fix` command:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql
```

## Individual Rule Documentation

For detailed information about each rule, including examples and configuration options, see the individual rule documentation files in the category subdirectories:

- [Correctness Rules](correctness/)
- [Safety Rules](safety/)
- [Security Rules](security/)
- [Performance Rules](performance/)
- [Naming Rules](naming/)
- [Style Rules](style/)
- [Transactions Rules](transactions/)
- [Schema Rules](schema/)
- [Debug Rules](debug/)

## Configuration

Rules can be configured in `tsqlrefine.json` or via ruleset files. See [Configuration Documentation](../configuration.md) for details.

### Disabling Rules

To disable specific rules, use a ruleset file:

```json
{
  "rules": [
    { "id": "avoid-select-star", "enabled": false }
  ]
}
```

### Preset Rulesets

TsqlRefine provides three preset rulesets:

- **recommended**: Balanced set of rules for general use (default)
- **strict**: All rules enabled for maximum code quality
- **security-only**: Only security and safety-critical rules

```powershell
dotnet run --project src/TsqlRefine.Cli -- lint --preset strict file.sql
```

## Contributing

To add a new rule, see [CLAUDE.md](../../CLAUDE.md#adding-a-new-built-in-rule) for implementation guidelines.
