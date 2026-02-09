# TsqlRefine Rules Reference

> NOTE: This file is generated automatically. Do not edit manually.
> For an overview and guide, see [README.md](README.md).

## Table of Contents

- [Rule Statistics](#rule-statistics)
- [Rule Categories](#rule-categories)
- [Rules by Category](#rules-by-category)
- [Rules by Severity](#rules-by-severity)
- [Fixable Rules](#fixable-rules)

## Rule Statistics

- **Total Rules**: 105
- **Fixable Rules**: 10 (10%)
- **Error Severity**: 13 rules (12%)
- **Warning Severity**: 57 rules (54%)
- **Information Severity**: 35 rules (33%)

## Rule Categories

| Category | Rules | Description |
|----------|-------|-------------|
| **Correctness** | 30 | Detects code that may produce incorrect results or runtime errors |
| **Safety** | 5 | Prevents destructive or dangerous operations |
| **Security** | 4 | Identifies security vulnerabilities like SQL injection |
| **Performance** | 19 | Flags patterns that can cause performance issues |
| **Style** | 29 | Maintains code formatting and consistency |
| **Transactions** | 10 | Ensures proper transaction handling and session settings |
| **Schema** | 7 | Enforces database schema best practices |
| **Debug** | 1 | Controls debug and output statements |

## Rules by Category

### Correctness (30 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-ambiguous-datetime-literal](correctness/avoid-ambiguous-datetime-literal.md) | Disallows slash-delimited date literals; they depend on language/locale and can silently change meaning - prefer ISO 8601. | Warning | No |
| [avoid-atat-identity](correctness/avoid-atat-identity.md) | Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT. | Warning | No |
| [avoid-float-for-decimal](correctness/avoid-float-for-decimal.md) | Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision. | Warning | No |
| [avoid-nolock](correctness/avoid-nolock.md) | Avoid using NOLOCK hint or READ UNCOMMITTED isolation level | Warning | No |
| [avoid-null-comparison](correctness/avoid-null-comparison.md) | Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN. | Warning | No |
| [ban-legacy-join-syntax](correctness/ban-legacy-join-syntax.md) | Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results. | Error | No |
| [escape-keyword-identifier](correctness/escape-keyword-identifier.md) | Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it. | Warning | **Yes** |
| [insert-select-column-name-mismatch](correctness/insert-select-column-name-mismatch.md) | Warns when INSERT target column names do not match SELECT output column names in INSERT ... SELECT statements. | Information | No |
| [named-constraint](correctness/named-constraint.md) | Prohibit named constraints in temp tables to avoid naming conflicts | Error | No |
| [no-top-without-order-by-in-select-into](correctness/no-top-without-order-by-in-select-into.md) | Detects SELECT TOP ... INTO without ORDER BY, which creates permanent tables with non-deterministic data. | Error | No |
| [order-by-in-subquery](correctness/order-by-in-subquery.md) | Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it. | Warning | No |
| [require-column-list-for-insert-select](correctness/require-column-list-for-insert-select.md) | INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes | Warning | No |
| [require-column-list-for-insert-values](correctness/require-column-list-for-insert-values.md) | INSERT VALUES statements must explicitly specify the column list to avoid errors when table schema changes | Warning | No |
| [require-parentheses-for-mixed-and-or](correctness/require-parentheses-for-mixed-and-or.md) | Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion. | Warning | No |
| [semantic/alias-scope-violation](correctness/semantic-alias-scope-violation.md) | Detects potential scope violations where aliases from outer queries are referenced in inner queries without clear correlation intent. | Warning | No |
| [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md) | Detects CTE name conflicts with other CTEs or table aliases in the same scope. | Error | No |
| [semantic/data-type-length](correctness/semantic-data-type-length.md) | Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY). | Error | No |
| [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md) | Detects duplicate table aliases in the same scope, which causes ambiguous references. | Error | No |
| [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md) | Detects column count mismatches between the target column list and the source in INSERT statements. | Error | No |
| [semantic/join-condition-always-true](correctness/semantic-join-condition-always-true.md) | Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons. | Warning | No |
| [semantic/join-table-not-referenced-in-on](correctness/semantic-join-table-not-referenced-in-on.md) | Detects JOIN operations where the joined table is not referenced in the ON clause. | Warning | No |
| [semantic/left-join-filtered-by-where](correctness/semantic-left-join-filtered-by-where.md) | Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN. | Warning | No |
| [semantic/return-after-statements](correctness/semantic-return-after-statements.md) | Detects unreachable statements after a RETURN statement in stored procedures or functions. | Warning | No |
| [semantic/set-variable](correctness/semantic-set-variable.md) | Recommends using SELECT for variable assignment instead of SET for consistency. | Warning | No |
| [semantic/undefined-alias](correctness/semantic-undefined-alias.md) | Detects references to undefined table aliases in column qualifiers. | Error | No |
| [string-agg-without-order-by](correctness/string-agg-without-order-by.md) | Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results. | Warning | No |
| [stuff-without-order-by](correctness/stuff-without-order-by.md) | Detects STUFF with FOR XML PATH that lacks ORDER BY, which may produce non-deterministic string concatenation results. | Warning | No |
| [semantic/unicode-string](correctness/semantic-unicode-string.md) | Detects Unicode characters in string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss. | Error | **Yes** |
| [union-type-mismatch](correctness/union-type-mismatch.md) | Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation. | Warning | No |
| [unreachable-case-when](correctness/unreachable-case-when.md) | Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable. | Warning | No |

### Safety (5 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-merge](safety/avoid-merge.md) | Avoid using MERGE statement due to known bugs (see KB 3180087, KB 4519788) | Warning | No |
| [cross-database-transaction](safety/cross-database-transaction.md) | Discourage cross-database transactions to avoid distributed transaction issues | Warning | No |
| [dangerous-ddl](safety/dangerous-ddl.md) | Detects destructive DDL operations (DROP, TRUNCATE, ALTER TABLE DROP) that can cause irreversible data loss. | Warning | No |
| [dml-without-where](safety/dml-without-where.md) | Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications. | Error | No |
| [require-drop-if-exists](safety/require-drop-if-exists.md) | Requires IF EXISTS on DROP statements for idempotent deployment scripts. | Information | No |

### Security (4 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-dangerous-procedures](security/avoid-dangerous-procedures.md) | Detects usage of dangerous extended stored procedures (xp_cmdshell, xp_reg*, sp_OA*) that pose security risks. | Warning | No |
| [avoid-exec-dynamic-sql](security/avoid-exec-dynamic-sql.md) | Detects EXEC with dynamic SQL (EXEC(...) pattern) which can be vulnerable to SQL injection | Warning | No |
| [avoid-execute-as](security/avoid-execute-as.md) | Detects EXECUTE AS clauses that change execution context, which can lead to unintended privilege escalation. | Information | No |
| [avoid-openrowset-opendatasource](security/avoid-openrowset-opendatasource.md) | Detects OPENROWSET and OPENDATASOURCE usage, which can be exploited for unauthorized remote data access. | Warning | No |

### Performance (19 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-implicit-conversion-in-predicate](performance/avoid-implicit-conversion-in-predicate.md) | Detects CAST or CONVERT applied to columns in predicates which can cause implicit type conversions and prevent index usage | Warning | No |
| [avoid-select-star](performance/avoid-select-star.md) | Avoid SELECT * in queries. | Warning | No |
| [avoid-top-in-dml](performance/avoid-top-in-dml.md) | Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy. | Warning | No |
| [ban-query-hints](performance/ban-query-hints.md) | Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues. | Warning | No |
| [data-compression](performance/data-compression.md) | Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization | Information | No |
| [disallow-cursors](performance/disallow-cursors.md) | Prohibit cursor usage; prefer set-based operations for better performance | Warning | No |
| [disallow-select-distinct](performance/disallow-select-distinct.md) | Flags SELECT DISTINCT usage which often masks JOIN bugs or missing GROUP BY, and has performance implications. | Information | No |
| [disallow-select-into](performance/disallow-select-into.md) | Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results. | Information | No |
| [forbid-top-100-percent-order-by](performance/forbid-top-100-percent-order-by.md) | Forbids TOP 100 PERCENT ORDER BY; it is redundant and often ignored by the optimizer. | Warning | No |
| [full-text](performance/full-text.md) | Prohibit full-text search predicates; use alternative search strategies for better performance | Information | No |
| [information-schema](performance/information-schema.md) | Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance | Information | No |
| [like-leading-wildcard](performance/like-leading-wildcard.md) | Detects LIKE patterns with a leading wildcard (%, _, [) in predicates, which prevents index usage and causes full table scans. | Warning | No |
| [linked-server](performance/linked-server.md) | Prohibit linked server queries (4-part identifiers); use alternative data access patterns | Information | No |
| [non-sargable](performance/non-sargable.md) | Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (non-sargable predicates) | Warning | No |
| [object-property](performance/object-property.md) | Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX or sys catalog views instead | Warning | No |
| [prefer-exists-over-in-subquery](performance/prefer-exists-over-in-subquery.md) | Detects WHERE column IN (SELECT ...) patterns and recommends EXISTS for potentially better performance with large datasets. | Information | No |
| [top-without-order-by](performance/top-without-order-by.md) | Detects TOP clause without ORDER BY, which produces non-deterministic results. | Warning | No |
| [upper-lower](performance/upper-lower.md) | Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage | Warning | No |
| [utc-datetime](performance/utc-datetime.md) | Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones | Warning | No |

### Style (29 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-magic-convert-style-for-datetime](style/avoid-magic-convert-style-for-datetime.md) | Warns on datetime CONVERT style numbers (magic numbers); encourages clearer, safer formatting patterns. | Information | No |
| [conditional-begin-end](style/conditional-begin-end.md) | Require BEGIN/END blocks in conditional statements for clarity and maintainability | Information | No |
| [duplicate-empty-line](style/duplicate-empty-line.md) | Avoid consecutive empty lines (more than one blank line in a row). | Information | No |
| [duplicate-go](style/duplicate-go.md) | Avoid consecutive GO batch separators. | Information | No |
| [join-keyword](style/join-keyword.md) | Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability | Warning | No |
| [nested-block-comments](style/nested-block-comments.md) | Avoid nested block comments (/* /* */ */). | Warning | No |
| [normalize-execute-keyword](style/normalize-execute-keyword.md) | Normalizes 'EXEC' to 'EXECUTE' for consistency. | Information | **Yes** |
| [normalize-procedure-keyword](style/normalize-procedure-keyword.md) | Normalizes 'PROC' to 'PROCEDURE' for consistency. | Information | **Yes** |
| [normalize-transaction-keyword](style/normalize-transaction-keyword.md) | Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK. | Information | **Yes** |
| [prefer-coalesce-over-nested-isnull](style/prefer-coalesce-over-nested-isnull.md) | Detects nested ISNULL and recommends COALESCE; reduces nesting and aligns with standard SQL behavior. | Information | No |
| [prefer-concat-over-plus](style/prefer-concat-over-plus.md) | Recommends CONCAT() when + concatenation uses ISNULL/COALESCE; avoids subtle NULL propagation (SQL Server 2012+). | Information | No |
| [prefer-concat-over-plus-when-nullable-or-convert](style/prefer-concat-over-plus-when-nullable-or-convert.md) | Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+). | Information | No |
| [prefer-concat-ws](style/prefer-concat-ws.md) | Recommends CONCAT_WS() when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+). | Information | No |
| [prefer-json-functions](style/prefer-json-functions.md) | Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+). | Information | No |
| [prefer-string-agg-over-stuff](style/prefer-string-agg-over-stuff.md) | Recommends STRING_AGG() over STUFF(... FOR XML PATH('') ...); simpler and typically faster/safer (SQL Server 2017+). | Information | No |
| [prefer-trim-over-ltrim-rtrim](style/prefer-trim-over-ltrim-rtrim.md) | Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+). | Information | No |
| [prefer-try-convert-patterns](style/prefer-try-convert-patterns.md) | Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE; fewer false positives and clearer intent. | Information | No |
| [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md) | Encourages Unicode string literals (N'...') to avoid encoding issues, using conservative safe-mode autofixes. | Information | **Yes** |
| [qualified-select-columns](style/qualified-select-columns.md) | Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap. | Information | No |
| [require-as-for-column-alias](style/require-as-for-column-alias.md) | Column aliases should use the AS keyword | Information | **Yes** |
| [require-as-for-table-alias](style/require-as-for-table-alias.md) | Table aliases should use the AS keyword | Information | **Yes** |
| [require-begin-end-for-if-with-controlflow-exception](style/require-begin-end-for-if-with-controlflow-exception.md) | Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block. | Warning | No |
| [require-begin-end-for-while](style/require-begin-end-for-while.md) | Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited. | Warning | No |
| [require-explicit-join-type](style/require-explicit-join-type.md) | Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase. | Warning | **Yes** |
| [require-qualified-columns-everywhere](style/require-qualified-columns-everywhere.md) | Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced; stricter than qualified-select-columns. | Warning | No |
| [semantic/case-sensitive-variables](style/semantic-case-sensitive-variables.md) | Ensures variable references match the exact casing used in their declarations for consistency. | Warning | No |
| [semantic/multi-table-alias](style/semantic-multi-table-alias.md) | Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity. | Warning | No |
| [semantic/schema-qualify](style/semantic-schema-qualify.md) | Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity. | Warning | No |
| [semicolon-termination](style/semicolon-termination.md) | SQL statements should be terminated with a semicolon | Information | **Yes** |

### Transactions (10 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [catch-swallowing](transactions/catch-swallowing.md) | Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures. | Warning | No |
| [require-save-transaction-in-nested](transactions/require-save-transaction-in-nested.md) | Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction. | Information | No |
| [require-try-catch-for-transaction](transactions/require-try-catch-for-transaction.md) | Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently. | Warning | No |
| [require-xact-abort-on](transactions/require-xact-abort-on.md) | Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work. | Warning | No |
| [set-ansi](transactions/set-ansi.md) | Files should start with SET ANSI_NULLS ON within the first 10 statements. | Warning | No |
| [set-nocount](transactions/set-nocount.md) | Files should start with SET NOCOUNT ON within the first 10 statements. | Warning | No |
| [set-quoted-identifier](transactions/set-quoted-identifier.md) | Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements. | Warning | No |
| [set-transaction-isolation-level](transactions/set-transaction-isolation-level.md) | Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements. | Warning | No |
| [transaction-without-commit-or-rollback](transactions/transaction-without-commit-or-rollback.md) | Detects BEGIN TRANSACTION statements without corresponding COMMIT or ROLLBACK in the same batch. | Error | No |
| [uncommitted-transaction](transactions/uncommitted-transaction.md) | BEGIN TRANSACTION requires corresponding COMMIT TRANSACTION in the same file | Warning | No |

### Schema (7 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-heap-table](schema/avoid-heap-table.md) | Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs. | Warning | No |
| [duplicate-column-definition](schema/duplicate-column-definition.md) | Detects duplicate column names in CREATE TABLE definitions; duplicate columns always cause a runtime error. | Error | No |
| [duplicate-foreign-key-column](schema/duplicate-foreign-key-column.md) | Detects duplicate columns within a single FOREIGN KEY constraint definition. | Warning | No |
| [duplicate-index-column](schema/duplicate-index-column.md) | Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint definition. | Warning | No |
| [duplicate-index-definition](schema/duplicate-index-definition.md) | Detects multiple indexes or unique constraints within a table that have the exact same column composition. | Warning | No |
| [require-ms-description-for-table-definition-file](schema/require-ms-description-for-table-definition-file.md) | Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL. | Information | No |
| [require-primary-key-or-unique-constraint](schema/require-primary-key-or-unique-constraint.md) | Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity. | Warning | No |

### Debug (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [print-statement](debug/print-statement.md) | Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging | Information | No |

## Rules by Severity

### Error (13 rules)

- [avoid-null-comparison](correctness/avoid-null-comparison.md)
- [ban-legacy-join-syntax](correctness/ban-legacy-join-syntax.md)
- [dml-without-where](safety/dml-without-where.md)
- [duplicate-column-definition](schema/duplicate-column-definition.md)
- [named-constraint](correctness/named-constraint.md)
- [no-top-without-order-by-in-select-into](correctness/no-top-without-order-by-in-select-into.md)
- [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md)
- [semantic/data-type-length](correctness/semantic-data-type-length.md)
- [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md)
- [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md)
- [semantic/undefined-alias](correctness/semantic-undefined-alias.md)
- [semantic/unicode-string](correctness/semantic-unicode-string.md)
- [transaction-without-commit-or-rollback](transactions/transaction-without-commit-or-rollback.md)

### Warning (57 rules)

- [avoid-ambiguous-datetime-literal](correctness/avoid-ambiguous-datetime-literal.md)
- [avoid-atat-identity](correctness/avoid-atat-identity.md)
- [avoid-dangerous-procedures](security/avoid-dangerous-procedures.md)
- [avoid-exec-dynamic-sql](security/avoid-exec-dynamic-sql.md)
- [avoid-execute-as](security/avoid-execute-as.md)
- [avoid-float-for-decimal](correctness/avoid-float-for-decimal.md)
- [avoid-heap-table](schema/avoid-heap-table.md)
- [avoid-implicit-conversion-in-predicate](performance/avoid-implicit-conversion-in-predicate.md)
- [avoid-merge](safety/avoid-merge.md)
- [avoid-nolock](correctness/avoid-nolock.md)
- [avoid-openrowset-opendatasource](security/avoid-openrowset-opendatasource.md)
- [avoid-select-star](performance/avoid-select-star.md)
- [avoid-top-in-dml](performance/avoid-top-in-dml.md)
- [ban-query-hints](performance/ban-query-hints.md)
- [catch-swallowing](transactions/catch-swallowing.md)
- [cross-database-transaction](safety/cross-database-transaction.md)
- [dangerous-ddl](safety/dangerous-ddl.md)
- [disallow-cursors](performance/disallow-cursors.md)
- [duplicate-foreign-key-column](schema/duplicate-foreign-key-column.md)
- [duplicate-index-column](schema/duplicate-index-column.md)
- [duplicate-index-definition](schema/duplicate-index-definition.md)
- [escape-keyword-identifier](correctness/escape-keyword-identifier.md)
- [forbid-top-100-percent-order-by](performance/forbid-top-100-percent-order-by.md)
- [join-keyword](style/join-keyword.md)
- [like-leading-wildcard](performance/like-leading-wildcard.md)
- [nested-block-comments](style/nested-block-comments.md)
- [non-sargable](performance/non-sargable.md)
- [object-property](performance/object-property.md)
- [order-by-in-subquery](correctness/order-by-in-subquery.md)
- [require-begin-end-for-if-with-controlflow-exception](style/require-begin-end-for-if-with-controlflow-exception.md)
- [require-begin-end-for-while](style/require-begin-end-for-while.md)
- [require-column-list-for-insert-select](correctness/require-column-list-for-insert-select.md)
- [require-column-list-for-insert-values](correctness/require-column-list-for-insert-values.md)
- [require-explicit-join-type](style/require-explicit-join-type.md)
- [require-parentheses-for-mixed-and-or](correctness/require-parentheses-for-mixed-and-or.md)
- [require-primary-key-or-unique-constraint](schema/require-primary-key-or-unique-constraint.md)
- [require-qualified-columns-everywhere](style/require-qualified-columns-everywhere.md)
- [require-try-catch-for-transaction](transactions/require-try-catch-for-transaction.md)
- [require-xact-abort-on](transactions/require-xact-abort-on.md)
- [semantic/alias-scope-violation](correctness/semantic-alias-scope-violation.md)
- [semantic/join-condition-always-true](correctness/semantic-join-condition-always-true.md)
- [semantic/join-table-not-referenced-in-on](correctness/semantic-join-table-not-referenced-in-on.md)
- [semantic/left-join-filtered-by-where](correctness/semantic-left-join-filtered-by-where.md)
- [semantic/multi-table-alias](style/semantic-multi-table-alias.md)
- [semantic/return-after-statements](correctness/semantic-return-after-statements.md)
- [semantic/schema-qualify](style/semantic-schema-qualify.md)
- [semantic/set-variable](correctness/semantic-set-variable.md)
- [set-ansi](transactions/set-ansi.md)
- [set-quoted-identifier](transactions/set-quoted-identifier.md)
- [string-agg-without-order-by](correctness/string-agg-without-order-by.md)
- [stuff-without-order-by](correctness/stuff-without-order-by.md)
- [top-without-order-by](performance/top-without-order-by.md)
- [uncommitted-transaction](transactions/uncommitted-transaction.md)
- [union-type-mismatch](correctness/union-type-mismatch.md)
- [unreachable-case-when](correctness/unreachable-case-when.md)
- [upper-lower](performance/upper-lower.md)
- [utc-datetime](performance/utc-datetime.md)

### Information (35 rules)

- [avoid-magic-convert-style-for-datetime](style/avoid-magic-convert-style-for-datetime.md)
- [conditional-begin-end](style/conditional-begin-end.md)
- [data-compression](performance/data-compression.md)
- [disallow-select-distinct](performance/disallow-select-distinct.md)
- [disallow-select-into](performance/disallow-select-into.md)
- [duplicate-empty-line](style/duplicate-empty-line.md)
- [duplicate-go](style/duplicate-go.md)
- [full-text](performance/full-text.md)
- [information-schema](performance/information-schema.md)
- [insert-select-column-name-mismatch](correctness/insert-select-column-name-mismatch.md)
- [linked-server](performance/linked-server.md)
- [normalize-execute-keyword](style/normalize-execute-keyword.md)
- [normalize-procedure-keyword](style/normalize-procedure-keyword.md)
- [normalize-transaction-keyword](style/normalize-transaction-keyword.md)
- [prefer-coalesce-over-nested-isnull](style/prefer-coalesce-over-nested-isnull.md)
- [prefer-concat-over-plus](style/prefer-concat-over-plus.md)
- [prefer-concat-over-plus-when-nullable-or-convert](style/prefer-concat-over-plus-when-nullable-or-convert.md)
- [prefer-concat-ws](style/prefer-concat-ws.md)
- [prefer-exists-over-in-subquery](performance/prefer-exists-over-in-subquery.md)
- [prefer-json-functions](style/prefer-json-functions.md)
- [prefer-string-agg-over-stuff](style/prefer-string-agg-over-stuff.md)
- [prefer-trim-over-ltrim-rtrim](style/prefer-trim-over-ltrim-rtrim.md)
- [prefer-try-convert-patterns](style/prefer-try-convert-patterns.md)
- [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md)
- [print-statement](debug/print-statement.md)
- [qualified-select-columns](style/qualified-select-columns.md)
- [require-as-for-column-alias](style/require-as-for-column-alias.md)
- [require-as-for-table-alias](style/require-as-for-table-alias.md)
- [require-drop-if-exists](safety/require-drop-if-exists.md)
- [require-ms-description-for-table-definition-file](schema/require-ms-description-for-table-definition-file.md)
- [require-save-transaction-in-nested](transactions/require-save-transaction-in-nested.md)
- [semicolon-termination](style/semicolon-termination.md)
- [semantic/case-sensitive-variables](style/semantic-case-sensitive-variables.md)
- [set-nocount](transactions/set-nocount.md)
- [set-transaction-isolation-level](transactions/set-transaction-isolation-level.md)

## Fixable Rules

The following 10 rules support automatic fixing:

1. [escape-keyword-identifier](correctness/escape-keyword-identifier.md) - Warns when a Transact-SQL keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.
2. [normalize-execute-keyword](style/normalize-execute-keyword.md) - Normalizes 'EXEC' to 'EXECUTE' for consistency.
3. [normalize-procedure-keyword](style/normalize-procedure-keyword.md) - Normalizes 'PROC' to 'PROCEDURE' for consistency.
4. [normalize-transaction-keyword](style/normalize-transaction-keyword.md) - Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK.
5. [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md) - Encourages Unicode string literals (N'...') to avoid encoding issues, using conservative safe-mode autofixes.
6. [require-as-for-column-alias](style/require-as-for-column-alias.md) - Column aliases should use the AS keyword
7. [require-as-for-table-alias](style/require-as-for-table-alias.md) - Table aliases should use the AS keyword
8. [require-explicit-join-type](style/require-explicit-join-type.md) - Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.
9. [semantic/unicode-string](correctness/semantic-unicode-string.md) - Detects Unicode characters in string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss.
10. [semicolon-termination](style/semicolon-termination.md) - SQL statements should be terminated with a semicolon

To apply auto-fixes, use the `fix` command:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql
```
