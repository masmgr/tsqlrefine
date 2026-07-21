# TsqlRefine Rules Reference

> NOTE: This file is generated automatically. Do not edit manually.
> For an overview and guide, see [README.md](README.md).

## Table of Contents

- [Rule Statistics](#rule-statistics)
- [Importance Tiers](#importance-tiers)
- [Rule Categories](#rule-categories)
- [Rules by Importance Tier](#rules-by-importance-tier)
  - [Critical (security-only)](#critical-security-only)
  - [Essential (pragmatic)](#essential-pragmatic)
  - [Recommended (recommended)](#recommended)
  - [Thorough (strict-logic)](#thorough-strict-logic)
  - [Cosmetic (strict)](#cosmetic-strict)
- [Rules by Severity](#rules-by-severity)
- [Fixable Rules](#fixable-rules)

## Rule Statistics

- **Total Rules**: 171
- **Fixable Rules**: 13 (8%)
- **By Importance Tier**:
  - Critical (security-only): 17 rules
  - Essential (pragmatic): 35 rules
  - Recommended (recommended): 61 rules
  - Thorough (strict-logic): 34 rules
  - Cosmetic (strict): 24 rules
- **By Severity**:
  - Error: 26 rules (15%)
  - Warning: 100 rules (58%)
  - Information: 45 rules (26%)

## Importance Tiers

Rules are organized into five importance tiers based on which preset first includes them. Each higher preset is a strict superset of the one below:

```
security-only ⊂ pragmatic ⊂ recommended ⊂ strict-logic ⊂ strict
```

| Tier | Preset | Rules | Cumulative | Description |
|------|--------|-------|------------|-------------|
| **Critical** | security-only | 17 | 17 | Security vulnerabilities and critical safety issues that can cause data loss or security breaches |
| **Essential** | pragmatic | 35 | 52 | Production-ready minimum for correctness and preventing runtime errors |
| **Recommended** | recommended | 61 | 113 | Balanced production use with semantic analysis and best practices |
| **Thorough** | strict-logic | 34 | 147 | Comprehensive correctness, performance, and schema checks without cosmetic style enforcement |
| **Cosmetic** | strict | 24 | 171 | Style consistency, formatting, and naming conventions for maximum code uniformity |

## Rule Categories

| Category | Rules | Description |
|----------|-------|-------------|
| **Security** | 7 | Identifies security vulnerabilities like SQL injection |
| **Safety** | 5 | Prevents destructive or dangerous operations |
| **Correctness** | 57 | Detects code that may produce incorrect results or runtime errors |
| **Performance** | 30 | Flags patterns that can cause performance issues |
| **Transactions** | 16 | Ensures proper transaction handling and session settings |
| **Schema** | 22 | Enforces database schema best practices |
| **Style** | 33 | Maintains code formatting and consistency |
| **Debug** | 1 | Controls debug and output statements |

## Rules by Importance Tier

### Critical (security-only)

**17 rules** — Security vulnerabilities and critical safety issues that can cause data loss or security breaches. These rules should never be disabled in production code.

#### Security (7 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-dangerous-procedures](security/avoid-dangerous-procedures.md) | Detects usage of dangerous extended stored procedures (xp_cmdshell, xp_reg*, sp_OA*) that pose security risks. | Warning | No |
| [avoid-exec-dynamic-sql](security/avoid-exec-dynamic-sql.md) | Detects EXEC with dynamic SQL (EXEC(...) pattern) which can be vulnerable to SQL injection | Warning | No |
| [avoid-execute-as](security/avoid-execute-as.md) | Detects EXECUTE AS usage for privilege escalation. EXECUTE AS can change the security context and may lead to unintended privilege escalation. | Warning | No |
| [avoid-hardcoded-password](security/avoid-hardcoded-password.md) | Detects hardcoded passwords in login DDL and ad hoc data-source connection strings. | Warning | No |
| [avoid-openrowset-opendatasource](security/avoid-openrowset-opendatasource.md) | Detects OPENROWSET and OPENDATASOURCE usage, which can be exploited for unauthorized remote data access. | Warning | No |
| [dynamic-sql-taint](security/dynamic-sql-taint.md) | Detects untrusted values that flow into dynamically executed SQL text. | Error | No |
| [require-parameterized-sp-executesql](security/require-parameterized-sp-executesql.md) | Detects sp_executesql calls without proper parameterization or with string concatenation. | Warning | No |

#### Safety (4 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-merge](safety/avoid-merge.md) | Avoid using MERGE statement due to known bugs (see KB 3180087, KB 4519788) | Warning | No |
| [cross-database-transaction](safety/cross-database-transaction.md) | Discourage cross-database transactions to avoid distributed transaction issues | Warning | No |
| [dangerous-ddl](safety/dangerous-ddl.md) | Detects destructive DDL operations (DROP, TRUNCATE, ALTER TABLE DROP) that can cause irreversible data loss. | Warning | No |
| [dml-without-where](safety/dml-without-where.md) | Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications. | Error | No |

#### Correctness (6 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [require-column-list-for-insert-select](correctness/require-column-list-for-insert-select.md) | INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes | Warning | No |
| [require-column-list-for-insert-values](correctness/require-column-list-for-insert-values.md) | INSERT VALUES statements must explicitly specify the column list to avoid errors when table schema changes | Warning | No |
| [require-semicolon-before-throw](correctness/require-semicolon-before-throw.md) | Requires the statement immediately before THROW to be terminated with a semicolon. | Error | No |
| [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md) | Detects duplicate table aliases in the same scope, which causes ambiguous references. | Error | No |
| [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md) | Detects column count mismatches between the target column list and the source in INSERT statements. | Error | No |
| [semantic/undefined-alias](correctness/semantic-undefined-alias.md) | Detects references to undefined table aliases in column qualifiers. | Error | No |

### Essential (pragmatic)

**35 rules** — Production-ready minimum for correctness and preventing runtime errors. Fundamental checks that catch bugs before they reach production.

#### Correctness (24 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [aggregate-in-where-clause](correctness/aggregate-in-where-clause.md) | Detects aggregate functions used directly in WHERE clauses. | Error | No |
| [avoid-ambiguous-datetime-literal](correctness/avoid-ambiguous-datetime-literal.md) | Disallows slash-delimited date literals; they depend on language/locale and can silently change meaning - prefer ISO 8601. | Warning | No |
| [avoid-atat-identity](correctness/avoid-atat-identity.md) | Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT. | Warning | No |
| [avoid-between-for-datetime-range](correctness/avoid-between-for-datetime-range.md) | Detects BETWEEN for datetime ranges. BETWEEN includes both endpoints, which can cause boundary issues with time components. | Warning | No |
| [avoid-legacy-join-syntax](correctness/avoid-legacy-join-syntax.md) | Detects legacy outer join syntax (*=, =*) which is deprecated and produces incorrect results. | Error | No |
| [avoid-named-constraint-in-temp-table](correctness/avoid-named-constraint-in-temp-table.md) | Prohibit named constraints in temp tables to avoid naming conflicts | Warning | No |
| [avoid-not-in-with-null](correctness/avoid-not-in-with-null.md) | Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values. | Warning | No |
| [avoid-null-comparison](correctness/avoid-null-comparison.md) | Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN. | Error | **Yes** |
| [avoid-set-rowcount](correctness/avoid-set-rowcount.md) | Detects SET ROWCOUNT statements which are deprecated and can cause unexpected behavior with triggers and nested statements. | Warning | No |
| [avoid-top-without-order-by-in-select-into](correctness/avoid-top-without-order-by-in-select-into.md) | Detects SELECT TOP ... INTO without ORDER BY, which may select non-deterministic rows. | Warning | No |
| [duplicate-insert-column](correctness/duplicate-insert-column.md) | Detects duplicate column names in INSERT column lists; duplicate columns always cause a runtime error. | Error | No |
| [exec-parameter-count-mismatch](correctness/exec-parameter-count-mismatch.md) | Detects EXEC calls with missing required or extra positional arguments. | Error | No |
| [exec-parameter-name-mismatch](correctness/exec-parameter-name-mismatch.md) | Detects named EXEC arguments that are absent from the procedure signature. | Error | No |
| [group-by-column-mismatch](correctness/group-by-column-mismatch.md) | Detects SELECT columns not contained in GROUP BY or an aggregate function. | Warning | No |
| [having-column-mismatch](correctness/having-column-mismatch.md) | Detects columns in HAVING clause not in GROUP BY and not wrapped in an aggregate function. | Warning | No |
| [insert-select-column-name-mismatch](correctness/insert-select-column-name-mismatch.md) | Warns when INSERT target column names do not match SELECT output column names in INSERT ... SELECT statements. | Information | No |
| [order-by-in-subquery](correctness/order-by-in-subquery.md) | Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it. | Warning | No |
| [require-parentheses-for-mixed-and-or](correctness/require-parentheses-for-mixed-and-or.md) | Detects mixed AND/OR operators at same precedence level without explicit parentheses to prevent precedence confusion. | Warning | No |
| [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md) | Detects CTE name conflicts with other CTEs or table aliases in the same scope. | Error | No |
| [semantic/data-type-length](correctness/semantic-data-type-length.md) | Requires explicit length specification for variable-length data types (VARCHAR, NVARCHAR, CHAR, NCHAR, VARBINARY, BINARY). | Error | No |
| [semantic/join-condition-always-true](correctness/semantic-join-condition-always-true.md) | Detects JOIN conditions that are always true or likely incorrect, such as 'ON 1=1' or self-comparisons. | Warning | No |
| [semantic/left-join-filtered-by-where](correctness/semantic-left-join-filtered-by-where.md) | Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN. | Warning | No |
| [union-type-mismatch](correctness/union-type-mismatch.md) | Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation. | Error | No |
| [unreachable-case-when](correctness/unreachable-case-when.md) | Detects duplicate WHEN conditions in CASE expressions that make later branches unreachable. | Warning | No |

#### Performance (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [top-without-order-by](performance/top-without-order-by.md) | Detects TOP clause without ORDER BY, which produces non-deterministic results. | Warning | No |

#### Transactions (2 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-transaction-without-commit](transactions/avoid-transaction-without-commit.md) | Detects BEGIN TRANSACTION statements without corresponding COMMIT or ROLLBACK in the same batch. | Error | No |
| [transaction-not-closed-on-path](transactions/transaction-not-closed-on-path.md) | Detects execution paths that leave a transaction opened by the current scope unclosed. | Error | No |

#### Schema (8 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-deprecated-types](schema/avoid-deprecated-types.md) | Detects deprecated TEXT, NTEXT, IMAGE, and TIMESTAMP data types and recommends modern replacements. | Warning | No |
| [delete-column-not-in-table](schema/delete-column-not-in-table.md) | Detects DELETE statements whose WHERE clause references columns not found in the target table. | Error | No |
| [duplicate-column-definition](schema/duplicate-column-definition.md) | Detects duplicate column names in CREATE TABLE definitions; duplicate columns always cause a runtime error. | Error | No |
| [duplicate-table-function-column](schema/duplicate-table-function-column.md) | Detects duplicate column names in table-valued function definitions; duplicate columns always cause a runtime error. | Error | No |
| [duplicate-table-variable-column](schema/duplicate-table-variable-column.md) | Detects duplicate column names in DECLARE @table TABLE variable definitions; duplicate columns always cause a runtime error. | Error | No |
| [duplicate-view-column](schema/duplicate-view-column.md) | Detects duplicate column names in CREATE VIEW definitions; duplicate columns always cause a runtime error. | Error | No |
| [insert-column-not-in-table](schema/insert-column-not-in-table.md) | Detects INSERT statements that reference columns not found in the target table. | Error | No |
| [update-column-not-in-table](schema/update-column-not-in-table.md) | Detects UPDATE statements that reference columns not found in the target table. | Error | No |

### Recommended

**61 rules** — Balanced production use with semantic analysis and best practices. This is the default preset, providing comprehensive validation without excessive noise.

#### Correctness (18 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-float-for-decimal](correctness/avoid-float-for-decimal.md) | Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision. | Warning | No |
| [avoid-max-plus-one-key-generation](correctness/avoid-max-plus-one-key-generation.md) | Detects MAX(...) plus a positive integer in assignments or DML values, which is unsafe for key generation. | Warning | No |
| [avoid-nolock](correctness/avoid-nolock.md) | Avoid using NOLOCK hint or READ UNCOMMITTED isolation level | Warning | No |
| [cursor-not-deallocated-on-path](correctness/cursor-not-deallocated-on-path.md) | Detects execution paths where an opened cursor is not deallocated. | Warning | No |
| [duplicate-select-column](correctness/duplicate-select-column.md) | Detects duplicate output column names in SELECT queries; may cause ambiguous column references. | Warning | No |
| [escape-keyword-identifier](correctness/escape-keyword-identifier.md) | Warns when a T-SQL soft keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it. | Warning | **Yes** |
| [exec-output-not-captured](correctness/exec-output-not-captured.md) | Detects EXEC calls that omit OUTPUT when passing an output parameter. | Warning | No |
| [exec-parameter-type-mismatch](correctness/exec-parameter-type-mismatch.md) | Detects EXEC arguments with a known type that may lose information when assigned to the procedure parameter. | Warning | No |
| [semantic/alias-scope-violation](correctness/semantic-alias-scope-violation.md) | Detects potential scope violations where aliases from outer queries are referenced in inner queries without clear correlation intent. | Warning | No |
| [semantic/join-table-not-referenced-in-on](correctness/semantic-join-table-not-referenced-in-on.md) | Detects JOIN operations where the joined table is not referenced in the ON clause. | Warning | No |
| [semantic/return-after-statements](correctness/semantic-return-after-statements.md) | Detects unreachable statements after a RETURN statement in stored procedures or functions. | Warning | No |
| [semantic/unicode-string](correctness/semantic-unicode-string.md) | Detects national string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss. | Error | **Yes** |
| [string-agg-nvarchar-max](correctness/string-agg-nvarchar-max.md) | Detects STRING_AGG whose first argument is not explicitly cast to NVARCHAR(MAX), which risks intermediate result truncation (8000-byte / 4000-char limit). | Warning | No |
| [string-agg-without-order-by](correctness/string-agg-without-order-by.md) | Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results. | Warning | No |
| [string-assignment-length-mismatch](correctness/string-assignment-length-mismatch.md) | Detects string assignments whose statically known maximum length exceeds the destination capacity. | Warning | No |
| [stuff-without-order-by](correctness/stuff-without-order-by.md) | Detects STUFF with FOR XML PATH that lacks ORDER BY, which may produce non-deterministic string concatenation results. | Warning | No |
| [unreachable-statement](correctness/unreachable-statement.md) | Detects statements that are unreachable after control transfer or in constant-false branches. | Warning | No |
| [variable-used-before-assignment](correctness/variable-used-before-assignment.md) | Detects variables read before assignment on every path reaching the use. | Warning | No |

#### Performance (13 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-correlated-subquery-in-select](performance/avoid-correlated-subquery-in-select.md) | Detects correlated scalar subqueries in SELECT list which execute once per row and cause severe performance degradation. | Warning | No |
| [avoid-cursors](performance/avoid-cursors.md) | Prohibit cursor usage; prefer set-based operations for better performance | Warning | No |
| [avoid-implicit-conversion-in-predicate](performance/avoid-implicit-conversion-in-predicate.md) | Detects CAST or CONVERT applied to columns in predicates which can cause implicit type conversions and prevent index usage | Warning | No |
| [avoid-non-sargable-predicate](performance/avoid-non-sargable-predicate.md) | Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (non-sargable predicates) | Warning | No |
| [avoid-optional-parameter-pattern](performance/avoid-optional-parameter-pattern.md) | Detects optional parameter patterns (@p IS NULL OR col = @p) and (col = ISNULL(@p, col)) which prevent index usage and cause plan instability. | Warning | No |
| [avoid-query-hints](performance/avoid-query-hints.md) | Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues. | Warning | No |
| [avoid-scalar-udf-in-query](performance/avoid-scalar-udf-in-query.md) | Detects user-defined scalar function calls in queries which execute row-by-row and cause severe performance degradation. | Warning | No |
| [avoid-select-star](performance/avoid-select-star.md) | Avoid SELECT * in queries. | Warning | No |
| [avoid-top-100-percent-order-by](performance/avoid-top-100-percent-order-by.md) | Forbids TOP 100 PERCENT ORDER BY; it is redundant and often ignored by the optimizer. | Warning | No |
| [avoid-top-in-dml](performance/avoid-top-in-dml.md) | Disallows TOP in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy. | Warning | No |
| [like-leading-wildcard](performance/like-leading-wildcard.md) | Detects LIKE patterns with a leading wildcard (%, _, [) in predicates, which prevents index usage and causes full table scans. | Warning | No |
| [prefer-exists-over-in-subquery](performance/prefer-exists-over-in-subquery.md) | Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets. | Information | No |
| [redundant-semi-join](performance/redundant-semi-join.md) | Detects IN or EXISTS predicates that duplicate an existing INNER JOIN to the same table and key. | Information | No |

#### Transactions (13 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-catch-swallowing](transactions/avoid-catch-swallowing.md) | Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures. | Warning | No |
| [require-save-transaction-in-nested](transactions/require-save-transaction-in-nested.md) | Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction. | Information | No |
| [require-throw-or-raiserror-in-catch](transactions/require-throw-or-raiserror-in-catch.md) | Detects CATCH blocks that do not propagate the error via THROW, RAISERROR, or RETURN with error code. | Information | No |
| [require-try-catch-for-transaction](transactions/require-try-catch-for-transaction.md) | Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently. | Warning | No |
| [set-ansi](transactions/set-ansi.md) | Files should start with SET ANSI_NULLS ON within the first 10 statements. | Warning | No |
| [set-ansi-padding](transactions/set-ansi-padding.md) | Files should start with SET ANSI_PADDING ON within the first 10 statements. | Warning | No |
| [set-ansi-warnings](transactions/set-ansi-warnings.md) | Files should start with SET ANSI_WARNINGS ON within the first 10 statements. | Warning | No |
| [set-arithabort](transactions/set-arithabort.md) | Files should start with SET ARITHABORT ON within the first 10 statements. | Warning | No |
| [set-concat-null-yields-null](transactions/set-concat-null-yields-null.md) | Files should start with SET CONCAT_NULL_YIELDS_NULL ON within the first 10 statements. | Warning | No |
| [set-nocount](transactions/set-nocount.md) | Files should start with SET NOCOUNT ON within the first 10 statements. | Information | No |
| [set-quoted-identifier](transactions/set-quoted-identifier.md) | Files should start with SET QUOTED_IDENTIFIER ON within the first 10 statements. | Warning | No |
| [set-transaction-isolation-level](transactions/set-transaction-isolation-level.md) | Files should start with SET TRANSACTION ISOLATION LEVEL within the first 10 statements. | Information | No |
| [set-xact-abort](transactions/set-xact-abort.md) | Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work. | Warning | No |

#### Schema (10 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [duplicate-foreign-key-column](schema/duplicate-foreign-key-column.md) | Detects duplicate columns within a single FOREIGN KEY constraint definition. | Warning | No |
| [duplicate-index-column](schema/duplicate-index-column.md) | Detects duplicate columns within a single index, PRIMARY KEY, or UNIQUE constraint definition. | Warning | No |
| [duplicate-index-definition](schema/duplicate-index-definition.md) | Detects multiple indexes or unique constraints within a table that have the exact same column composition. | Warning | No |
| [implicit-conversion-in-predicate-schema](schema/implicit-conversion-in-predicate-schema.md) | Detects implicit type conversions on columns in predicates using schema type information. | Warning | No |
| [index-column-not-in-table](schema/index-column-not-in-table.md) | Detects index definitions that reference columns not found in the target table. | Error | No |
| [join-column-deviation](schema/join-column-deviation.md) | Detects JOINs where the column combination deviates from the dominant pattern observed in the relation profile. | Warning | No |
| [join-foreign-key-mismatch](schema/join-foreign-key-mismatch.md) | Detects JOINs where the ON columns match a foreign key relationship but the joined table differs from the FK target. | Warning | No |
| [unresolved-column-reference](schema/unresolved-column-reference.md) | Detects references to columns that do not exist in the schema snapshot. | Warning | No |
| [unresolved-table-reference](schema/unresolved-table-reference.md) | Detects references to tables or views that do not exist in the schema snapshot. | Warning | No |
| [update-join-cardinality-mismatch](schema/update-join-cardinality-mismatch.md) | Detects UPDATE...FROM...JOIN where the join may produce multiple rows per target row, causing non-deterministic updates. | Warning | No |

#### Style (7 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-order-by-ordinal](style/avoid-order-by-ordinal.md) | Forbids ORDER BY with ordinal positions (e.g., ORDER BY 1, 2) which break silently when columns are reordered. | Information | No |
| [prefer-eomonth-over-date-arithmetic](style/prefer-eomonth-over-date-arithmetic.md) | Recommends EOMONTH over common DATEADD-based month-end calculations. | Information | No |
| [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md) | Encourages Unicode string literals (N'...') to avoid encoding issues, using conservative safe-mode autofixes. | Information | **Yes** |
| [require-qualified-columns-everywhere](style/require-qualified-columns-everywhere.md) | Requires column qualification in WHERE / JOIN / ORDER BY when multiple tables are referenced; stricter than qualified-select-columns. | Warning | No |
| [require-schema-qualify-exec](style/require-schema-qualify-exec.md) | Requires schema qualification on EXEC procedure calls (e.g., EXEC dbo.ProcName instead of EXEC ProcName). | Warning | No |
| [semantic/multi-table-alias](style/semantic-multi-table-alias.md) | Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity. | Warning | No |
| [semantic/schema-qualify](style/semantic-schema-qualify.md) | Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity. | Warning | No |

### Thorough (strict-logic)

**34 rules** — Comprehensive correctness, performance, and schema checks without cosmetic style enforcement.

#### Safety (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [require-drop-if-exists](safety/require-drop-if-exists.md) | Requires IF EXISTS on DROP statements for idempotent deployment scripts. | Information | No |

#### Correctness (9 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [circular-object-reference](correctness/circular-object-reference.md) | Detects cycles between cataloged SQL objects. | Warning | No |
| [inconsistent-result-set](correctness/inconsistent-result-set.md) | Detects procedures that return different result-set shapes on different execution paths. | Warning | No |
| [len-for-emptiness-check](correctness/len-for-emptiness-check.md) | Warns when LEN() is used in an emptiness comparison; trailing spaces are ignored, so use DATALENGTH() to detect whitespace-only values. | Warning | No |
| [mixed-string-length-functions-in-loop](correctness/mixed-string-length-functions-in-loop.md) | Detects WHILE loops that use DATALENGTH for termination but LEN to advance the same string variable. | Warning | No |
| [multi-row-update-from](correctness/multi-row-update-from.md) | Warns on UPDATE...FROM with a JOIN, which can match multiple rows per target row and produce non-deterministic updates. | Warning | No |
| [semantic/set-variable](correctness/semantic-set-variable.md) | Recommends using SELECT for variable assignment instead of SET for consistency. | Warning | No |
| [unreferenced-object](correctness/unreferenced-object.md) | Detects cataloged SQL objects that have no incoming references. | Information | No |
| [unresolved-procedure-reference](correctness/unresolved-procedure-reference.md) | Detects procedure or function calls that do not resolve in an authoritative object catalog. | Warning | No |
| [unused-variable](correctness/unused-variable.md) | Detects local variables and routine parameters that are never read. | Information | No |

#### Performance (16 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-full-text-search](performance/avoid-full-text-search.md) | Prohibit full-text search predicates; use alternative search strategies for better performance | Information | No |
| [avoid-information-schema](performance/avoid-information-schema.md) | Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance | Information | No |
| [avoid-linked-server](performance/avoid-linked-server.md) | Prohibit linked server queries (4-part identifiers); use alternative data access patterns | Information | No |
| [avoid-objectproperty](performance/avoid-objectproperty.md) | Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX or sys catalog views instead | Warning | No |
| [avoid-or-on-different-columns](performance/avoid-or-on-different-columns.md) | Detects OR conditions on different columns in predicates which may prevent index usage and cause table scans. | Information | No |
| [avoid-select-distinct](performance/avoid-select-distinct.md) | Flags SELECT DISTINCT usage which often masks JOIN bugs or missing GROUP BY, and has performance implications. | Information | No |
| [avoid-select-into](performance/avoid-select-into.md) | Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results. | Information | No |
| [avoid-upper-lower-in-predicate](performance/avoid-upper-lower-in-predicate.md) | Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage | Warning | No |
| [deep-view-nesting](performance/deep-view-nesting.md) | Detects views whose dependency nesting exceeds a configured maximum. | Warning | No |
| [max-cyclomatic-complexity](performance/max-cyclomatic-complexity.md) | Limits cyclomatic complexity per SQL object or batch. | Warning | No |
| [max-joins-per-query](performance/max-joins-per-query.md) | Limits the number of joins in a single query. | Warning | No |
| [max-nesting-depth](performance/max-nesting-depth.md) | Limits control-flow nesting depth per SQL object or batch. | Warning | No |
| [max-parameter-count](performance/max-parameter-count.md) | Limits parameter count per procedure or function. | Information | No |
| [max-statement-count](performance/max-statement-count.md) | Limits executable statement count per SQL object or batch. | Information | No |
| [prefer-utc-datetime](performance/prefer-utc-datetime.md) | Detects local datetime functions (GETDATE, SYSDATETIME, CURRENT_TIMESTAMP, SYSDATETIMEOFFSET) and suggests UTC alternatives for consistency across time zones | Warning | No |
| [require-data-compression](performance/require-data-compression.md) | Recommend specifying DATA_COMPRESSION option in CREATE TABLE for storage optimization | Information | No |

#### Transactions (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [uncommitted-transaction](transactions/uncommitted-transaction.md) | BEGIN TRANSACTION requires corresponding COMMIT TRANSACTION in the same file | Warning | No |

#### Schema (3 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-heap-table](schema/avoid-heap-table.md) | Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs. | Warning | No |
| [require-primary-key-or-unique-constraint](schema/require-primary-key-or-unique-constraint.md) | Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity. | Warning | No |
| [require-table-description](schema/require-table-description.md) | Ensures table definition files include an MS_Description extended property so schema intent is captured alongside DDL. | Information | No |

#### Style (4 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [normalize-inequality-operator](style/normalize-inequality-operator.md) | Normalizes != to <> (ISO standard inequality operator). | Information | **Yes** |
| [prefer-concat-with-nullable](style/prefer-concat-with-nullable.md) | Stricter variant that also detects CAST/CONVERT in concatenations; enable instead of prefer-concat-over-plus for comprehensive coverage (SQL Server 2012+). | Information | No |
| [qualified-select-columns](style/qualified-select-columns.md) | Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle 'wrong table' mistakes when column names overlap. | Information | No |
| [semantic/case-sensitive-variables](style/semantic-case-sensitive-variables.md) | Ensures variable references match the exact casing used in their declarations for consistency. | Information | No |

### Cosmetic (strict)

**24 rules** — Style consistency, formatting, and naming conventions for maximum code uniformity.

#### Schema (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [require-named-default-constraint](schema/require-named-default-constraint.md) | Requires DEFAULT constraints on permanent table columns to have explicit names. | Warning | No |

#### Style (22 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-magic-convert-style-for-datetime](style/avoid-magic-convert-style-for-datetime.md) | Warns on datetime CONVERT style numbers (magic numbers); encourages clearer, safer formatting patterns. | Information | No |
| [duplicate-empty-line](style/duplicate-empty-line.md) | Avoid consecutive empty lines (more than one blank line in a row). | Information | No |
| [duplicate-go](style/duplicate-go.md) | Avoid consecutive GO batch separators. | Information | No |
| [nested-block-comments](style/nested-block-comments.md) | Avoid nested block comments (/* /* */ */). | Warning | No |
| [normalize-execute-keyword](style/normalize-execute-keyword.md) | Normalizes 'EXEC' to 'EXECUTE' for consistency. | Information | **Yes** |
| [normalize-procedure-keyword](style/normalize-procedure-keyword.md) | Normalizes 'PROC' to 'PROCEDURE' for consistency. | Information | **Yes** |
| [normalize-transaction-keyword](style/normalize-transaction-keyword.md) | Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK. | Information | **Yes** |
| [prefer-coalesce-over-nested-isnull](style/prefer-coalesce-over-nested-isnull.md) | Detects nested ISNULL and recommends COALESCE; reduces nesting and aligns with standard SQL behavior. | Information | No |
| [prefer-concat-over-plus](style/prefer-concat-over-plus.md) | Recommends CONCAT() when + concatenation uses ISNULL/COALESCE; avoids subtle NULL propagation (SQL Server 2012+). | Information | No |
| [prefer-concat-ws](style/prefer-concat-ws.md) | Recommends CONCAT_WS() when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+). | Information | No |
| [prefer-json-functions](style/prefer-json-functions.md) | Encourages built-in JSON features (OPENJSON, JSON_VALUE, FOR JSON, etc.) over manual string parsing/building (SQL Server 2016+). | Information | No |
| [prefer-string-agg-over-stuff](style/prefer-string-agg-over-stuff.md) | Recommends STRING_AGG() over STUFF(... FOR XML PATH('') ...); simpler and typically faster/safer (SQL Server 2017+). | Information | No |
| [prefer-trim-over-ltrim-rtrim](style/prefer-trim-over-ltrim-rtrim.md) | Recommends TRIM(x) instead of LTRIM(RTRIM(x)); clearer and less error-prone (SQL Server 2017+). | Information | No |
| [prefer-try-convert-patterns](style/prefer-try-convert-patterns.md) | Recommends TRY_CONVERT/TRY_CAST over CASE + ISNUMERIC/ISDATE; fewer false positives and clearer intent. | Information | No |
| [require-as-for-column-alias](style/require-as-for-column-alias.md) | Column aliases should use the AS keyword | Information | **Yes** |
| [require-as-for-table-alias](style/require-as-for-table-alias.md) | Table aliases should use the AS keyword | Information | **Yes** |
| [require-begin-end-for-while](style/require-begin-end-for-while.md) | Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited. | Warning | No |
| [require-begin-end-lenient](style/require-begin-end-lenient.md) | Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block. | Warning | No |
| [require-begin-end-strict](style/require-begin-end-strict.md) | Require BEGIN/END blocks in conditional statements for clarity and maintainability | Information | **Yes** |
| [require-explicit-join](style/require-explicit-join.md) | Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability | Warning | No |
| [require-explicit-join-type](style/require-explicit-join-type.md) | Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase. | Warning | **Yes** |
| [semicolon-termination](style/semicolon-termination.md) | SQL statements should be terminated with a semicolon | Information | **Yes** |

#### Debug (1 rules)

| Rule ID | Description | Severity | Fixable |
|---------|-------------|----------|---------|
| [avoid-print-statement](debug/avoid-print-statement.md) | Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging | Information | No |

## Rules by Severity

### Error (26 rules)

- [aggregate-in-where-clause](correctness/aggregate-in-where-clause.md)
- [avoid-legacy-join-syntax](correctness/avoid-legacy-join-syntax.md)
- [avoid-null-comparison](correctness/avoid-null-comparison.md)
- [avoid-transaction-without-commit](transactions/avoid-transaction-without-commit.md)
- [delete-column-not-in-table](schema/delete-column-not-in-table.md)
- [dml-without-where](safety/dml-without-where.md)
- [duplicate-column-definition](schema/duplicate-column-definition.md)
- [duplicate-insert-column](correctness/duplicate-insert-column.md)
- [duplicate-table-function-column](schema/duplicate-table-function-column.md)
- [duplicate-table-variable-column](schema/duplicate-table-variable-column.md)
- [duplicate-view-column](schema/duplicate-view-column.md)
- [dynamic-sql-taint](security/dynamic-sql-taint.md)
- [exec-parameter-count-mismatch](correctness/exec-parameter-count-mismatch.md)
- [exec-parameter-name-mismatch](correctness/exec-parameter-name-mismatch.md)
- [index-column-not-in-table](schema/index-column-not-in-table.md)
- [insert-column-not-in-table](schema/insert-column-not-in-table.md)
- [require-semicolon-before-throw](correctness/require-semicolon-before-throw.md)
- [semantic/cte-name-conflict](correctness/semantic-cte-name-conflict.md)
- [semantic/data-type-length](correctness/semantic-data-type-length.md)
- [semantic/duplicate-alias](correctness/semantic-duplicate-alias.md)
- [semantic/insert-column-count-mismatch](correctness/semantic-insert-column-count-mismatch.md)
- [semantic/undefined-alias](correctness/semantic-undefined-alias.md)
- [semantic/unicode-string](correctness/semantic-unicode-string.md)
- [transaction-not-closed-on-path](transactions/transaction-not-closed-on-path.md)
- [union-type-mismatch](correctness/union-type-mismatch.md)
- [update-column-not-in-table](schema/update-column-not-in-table.md)

### Warning (100 rules)

- [avoid-ambiguous-datetime-literal](correctness/avoid-ambiguous-datetime-literal.md)
- [avoid-atat-identity](correctness/avoid-atat-identity.md)
- [avoid-between-for-datetime-range](correctness/avoid-between-for-datetime-range.md)
- [avoid-catch-swallowing](transactions/avoid-catch-swallowing.md)
- [avoid-correlated-subquery-in-select](performance/avoid-correlated-subquery-in-select.md)
- [avoid-cursors](performance/avoid-cursors.md)
- [avoid-dangerous-procedures](security/avoid-dangerous-procedures.md)
- [avoid-deprecated-types](schema/avoid-deprecated-types.md)
- [avoid-exec-dynamic-sql](security/avoid-exec-dynamic-sql.md)
- [avoid-execute-as](security/avoid-execute-as.md)
- [avoid-float-for-decimal](correctness/avoid-float-for-decimal.md)
- [avoid-hardcoded-password](security/avoid-hardcoded-password.md)
- [avoid-heap-table](schema/avoid-heap-table.md)
- [avoid-implicit-conversion-in-predicate](performance/avoid-implicit-conversion-in-predicate.md)
- [avoid-max-plus-one-key-generation](correctness/avoid-max-plus-one-key-generation.md)
- [avoid-merge](safety/avoid-merge.md)
- [avoid-named-constraint-in-temp-table](correctness/avoid-named-constraint-in-temp-table.md)
- [avoid-nolock](correctness/avoid-nolock.md)
- [avoid-non-sargable-predicate](performance/avoid-non-sargable-predicate.md)
- [avoid-not-in-with-null](correctness/avoid-not-in-with-null.md)
- [avoid-objectproperty](performance/avoid-objectproperty.md)
- [avoid-openrowset-opendatasource](security/avoid-openrowset-opendatasource.md)
- [avoid-optional-parameter-pattern](performance/avoid-optional-parameter-pattern.md)
- [avoid-query-hints](performance/avoid-query-hints.md)
- [avoid-scalar-udf-in-query](performance/avoid-scalar-udf-in-query.md)
- [avoid-select-star](performance/avoid-select-star.md)
- [avoid-set-rowcount](correctness/avoid-set-rowcount.md)
- [avoid-top-100-percent-order-by](performance/avoid-top-100-percent-order-by.md)
- [avoid-top-in-dml](performance/avoid-top-in-dml.md)
- [avoid-top-without-order-by-in-select-into](correctness/avoid-top-without-order-by-in-select-into.md)
- [avoid-upper-lower-in-predicate](performance/avoid-upper-lower-in-predicate.md)
- [circular-object-reference](correctness/circular-object-reference.md)
- [cross-database-transaction](safety/cross-database-transaction.md)
- [cursor-not-deallocated-on-path](correctness/cursor-not-deallocated-on-path.md)
- [dangerous-ddl](safety/dangerous-ddl.md)
- [deep-view-nesting](performance/deep-view-nesting.md)
- [duplicate-foreign-key-column](schema/duplicate-foreign-key-column.md)
- [duplicate-index-column](schema/duplicate-index-column.md)
- [duplicate-index-definition](schema/duplicate-index-definition.md)
- [duplicate-select-column](correctness/duplicate-select-column.md)
- [escape-keyword-identifier](correctness/escape-keyword-identifier.md)
- [exec-output-not-captured](correctness/exec-output-not-captured.md)
- [exec-parameter-type-mismatch](correctness/exec-parameter-type-mismatch.md)
- [group-by-column-mismatch](correctness/group-by-column-mismatch.md)
- [having-column-mismatch](correctness/having-column-mismatch.md)
- [implicit-conversion-in-predicate-schema](schema/implicit-conversion-in-predicate-schema.md)
- [inconsistent-result-set](correctness/inconsistent-result-set.md)
- [join-column-deviation](schema/join-column-deviation.md)
- [join-foreign-key-mismatch](schema/join-foreign-key-mismatch.md)
- [len-for-emptiness-check](correctness/len-for-emptiness-check.md)
- [like-leading-wildcard](performance/like-leading-wildcard.md)
- [max-cyclomatic-complexity](performance/max-cyclomatic-complexity.md)
- [max-joins-per-query](performance/max-joins-per-query.md)
- [max-nesting-depth](performance/max-nesting-depth.md)
- [mixed-string-length-functions-in-loop](correctness/mixed-string-length-functions-in-loop.md)
- [multi-row-update-from](correctness/multi-row-update-from.md)
- [nested-block-comments](style/nested-block-comments.md)
- [order-by-in-subquery](correctness/order-by-in-subquery.md)
- [prefer-utc-datetime](performance/prefer-utc-datetime.md)
- [require-begin-end-for-while](style/require-begin-end-for-while.md)
- [require-begin-end-lenient](style/require-begin-end-lenient.md)
- [require-column-list-for-insert-select](correctness/require-column-list-for-insert-select.md)
- [require-column-list-for-insert-values](correctness/require-column-list-for-insert-values.md)
- [require-explicit-join](style/require-explicit-join.md)
- [require-explicit-join-type](style/require-explicit-join-type.md)
- [require-named-default-constraint](schema/require-named-default-constraint.md)
- [require-parameterized-sp-executesql](security/require-parameterized-sp-executesql.md)
- [require-parentheses-for-mixed-and-or](correctness/require-parentheses-for-mixed-and-or.md)
- [require-primary-key-or-unique-constraint](schema/require-primary-key-or-unique-constraint.md)
- [require-qualified-columns-everywhere](style/require-qualified-columns-everywhere.md)
- [require-schema-qualify-exec](style/require-schema-qualify-exec.md)
- [require-try-catch-for-transaction](transactions/require-try-catch-for-transaction.md)
- [semantic/alias-scope-violation](correctness/semantic-alias-scope-violation.md)
- [semantic/join-condition-always-true](correctness/semantic-join-condition-always-true.md)
- [semantic/join-table-not-referenced-in-on](correctness/semantic-join-table-not-referenced-in-on.md)
- [semantic/left-join-filtered-by-where](correctness/semantic-left-join-filtered-by-where.md)
- [semantic/multi-table-alias](style/semantic-multi-table-alias.md)
- [semantic/return-after-statements](correctness/semantic-return-after-statements.md)
- [semantic/schema-qualify](style/semantic-schema-qualify.md)
- [semantic/set-variable](correctness/semantic-set-variable.md)
- [set-ansi](transactions/set-ansi.md)
- [set-ansi-padding](transactions/set-ansi-padding.md)
- [set-ansi-warnings](transactions/set-ansi-warnings.md)
- [set-arithabort](transactions/set-arithabort.md)
- [set-concat-null-yields-null](transactions/set-concat-null-yields-null.md)
- [set-quoted-identifier](transactions/set-quoted-identifier.md)
- [set-xact-abort](transactions/set-xact-abort.md)
- [string-agg-nvarchar-max](correctness/string-agg-nvarchar-max.md)
- [string-agg-without-order-by](correctness/string-agg-without-order-by.md)
- [string-assignment-length-mismatch](correctness/string-assignment-length-mismatch.md)
- [stuff-without-order-by](correctness/stuff-without-order-by.md)
- [top-without-order-by](performance/top-without-order-by.md)
- [uncommitted-transaction](transactions/uncommitted-transaction.md)
- [unreachable-case-when](correctness/unreachable-case-when.md)
- [unreachable-statement](correctness/unreachable-statement.md)
- [unresolved-column-reference](schema/unresolved-column-reference.md)
- [unresolved-procedure-reference](correctness/unresolved-procedure-reference.md)
- [unresolved-table-reference](schema/unresolved-table-reference.md)
- [update-join-cardinality-mismatch](schema/update-join-cardinality-mismatch.md)
- [variable-used-before-assignment](correctness/variable-used-before-assignment.md)

### Information (45 rules)

- [avoid-full-text-search](performance/avoid-full-text-search.md)
- [avoid-information-schema](performance/avoid-information-schema.md)
- [avoid-linked-server](performance/avoid-linked-server.md)
- [avoid-magic-convert-style-for-datetime](style/avoid-magic-convert-style-for-datetime.md)
- [avoid-or-on-different-columns](performance/avoid-or-on-different-columns.md)
- [avoid-order-by-ordinal](style/avoid-order-by-ordinal.md)
- [avoid-print-statement](debug/avoid-print-statement.md)
- [avoid-select-distinct](performance/avoid-select-distinct.md)
- [avoid-select-into](performance/avoid-select-into.md)
- [duplicate-empty-line](style/duplicate-empty-line.md)
- [duplicate-go](style/duplicate-go.md)
- [insert-select-column-name-mismatch](correctness/insert-select-column-name-mismatch.md)
- [max-parameter-count](performance/max-parameter-count.md)
- [max-statement-count](performance/max-statement-count.md)
- [normalize-execute-keyword](style/normalize-execute-keyword.md)
- [normalize-inequality-operator](style/normalize-inequality-operator.md)
- [normalize-procedure-keyword](style/normalize-procedure-keyword.md)
- [normalize-transaction-keyword](style/normalize-transaction-keyword.md)
- [prefer-coalesce-over-nested-isnull](style/prefer-coalesce-over-nested-isnull.md)
- [prefer-concat-over-plus](style/prefer-concat-over-plus.md)
- [prefer-concat-with-nullable](style/prefer-concat-with-nullable.md)
- [prefer-concat-ws](style/prefer-concat-ws.md)
- [prefer-eomonth-over-date-arithmetic](style/prefer-eomonth-over-date-arithmetic.md)
- [prefer-exists-over-in-subquery](performance/prefer-exists-over-in-subquery.md)
- [prefer-json-functions](style/prefer-json-functions.md)
- [prefer-string-agg-over-stuff](style/prefer-string-agg-over-stuff.md)
- [prefer-trim-over-ltrim-rtrim](style/prefer-trim-over-ltrim-rtrim.md)
- [prefer-try-convert-patterns](style/prefer-try-convert-patterns.md)
- [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md)
- [qualified-select-columns](style/qualified-select-columns.md)
- [redundant-semi-join](performance/redundant-semi-join.md)
- [require-as-for-column-alias](style/require-as-for-column-alias.md)
- [require-as-for-table-alias](style/require-as-for-table-alias.md)
- [require-begin-end-strict](style/require-begin-end-strict.md)
- [require-data-compression](performance/require-data-compression.md)
- [require-drop-if-exists](safety/require-drop-if-exists.md)
- [require-save-transaction-in-nested](transactions/require-save-transaction-in-nested.md)
- [require-table-description](schema/require-table-description.md)
- [require-throw-or-raiserror-in-catch](transactions/require-throw-or-raiserror-in-catch.md)
- [semantic/case-sensitive-variables](style/semantic-case-sensitive-variables.md)
- [semicolon-termination](style/semicolon-termination.md)
- [set-nocount](transactions/set-nocount.md)
- [set-transaction-isolation-level](transactions/set-transaction-isolation-level.md)
- [unreferenced-object](correctness/unreferenced-object.md)
- [unused-variable](correctness/unused-variable.md)

## Fixable Rules

The following 13 rules support automatic fixing:

1. [avoid-null-comparison](correctness/avoid-null-comparison.md) - Detects NULL comparisons using = or <> instead of IS NULL/IS NOT NULL, which always evaluate to UNKNOWN.
2. [escape-keyword-identifier](correctness/escape-keyword-identifier.md) - Warns when a T-SQL soft keyword is used as a table/column identifier without escaping, and offers an autofix to bracket it.
3. [normalize-execute-keyword](style/normalize-execute-keyword.md) - Normalizes 'EXEC' to 'EXECUTE' for consistency.
4. [normalize-inequality-operator](style/normalize-inequality-operator.md) - Normalizes != to <> (ISO standard inequality operator).
5. [normalize-procedure-keyword](style/normalize-procedure-keyword.md) - Normalizes 'PROC' to 'PROCEDURE' for consistency.
6. [normalize-transaction-keyword](style/normalize-transaction-keyword.md) - Normalizes 'TRAN' to 'TRANSACTION' and requires explicit 'TRANSACTION' after COMMIT/ROLLBACK.
7. [prefer-unicode-string-literals](style/prefer-unicode-string-literals.md) - Encourages Unicode string literals (N'...') to avoid encoding issues, using conservative safe-mode autofixes.
8. [require-as-for-column-alias](style/require-as-for-column-alias.md) - Column aliases should use the AS keyword
9. [require-as-for-table-alias](style/require-as-for-table-alias.md) - Table aliases should use the AS keyword
10. [require-begin-end-strict](style/require-begin-end-strict.md) - Require BEGIN/END blocks in conditional statements for clarity and maintainability
11. [require-explicit-join-type](style/require-explicit-join-type.md) - Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.
12. [semantic/unicode-string](correctness/semantic-unicode-string.md) - Detects national string literals assigned to non-Unicode (VARCHAR/CHAR) variables, which may cause data loss.
13. [semicolon-termination](style/semicolon-termination.md) - SQL statements should be terminated with a semicolon

To apply auto-fixes, use the `fix` command:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --write file.sql
```
