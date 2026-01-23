# Rules List

This page is a catalog of rules provided by this plugin. Each item links to a per-rule page with examples, severity, and the exact message.

## General Notes / Cautions

- These rules intentionally encode "best practices" and team preferences; it is normal to enable/disable rules per project.
- Some rules can be noisy for one-off scripts, code generation, or migration tooling; consider scoping linting to the SQL you want to standardize.
- SQL Server version matters for some recommendations (e.g., `STRING_AGG`, `TRIM`, `CONCAT_WS`, JSON functions). If you target older versions, disable those rules.
- A few rules overlap in intent. Choose based on your needs:
  - ~~`avoid-nolock-or-read-uncommitted`~~ → **DEPRECATED:** Use [`avoid-nolock`](avoid-nolock.md) instead
  - [`prefer-concat-over-plus`](prefer-concat-over-plus.md) vs [`prefer-concat-over-plus-when-nullable-or-convert`](prefer-concat-over-plus-when-nullable-or-convert.md): The latter is stricter and includes type conversions
  - [`qualified-select-columns`](qualified-select-columns.md) vs [`require-qualified-columns-everywhere`](require-qualified-columns-everywhere.md): Use both together or choose based on strictness preference

## Table of Contents

- [Code Style & Naming Conventions](#code-style--naming-conventions) (3 rules)
- [Control Flow Safety](#control-flow-safety) (2 rules)
- [Query Structure & Clarity](#query-structure--clarity) (9 rules)
- [Schema Design](#schema-design) (4 rules)
- [Performance & Correctness](#performance--correctness) (5 rules)
- [Security](#security) (2 rules)
- [Data Access & Isolation](#data-access--isolation) (1 rule)
- [Transaction Safety](#transaction-safety) (2 rules)
- [Functions & Built-in Utilities](#functions--built-in-utilities) (10 rules)

**Total: 38 active rules** (1 deprecated: `avoid-nolock-or-read-uncommitted`)

## Code Style & Naming Conventions

- [`require-as-for-column-alias`](require-as-for-column-alias.md): Requires `AS` for column aliases to make intent explicit and reduce "is this an alias?" ambiguity.
- [`require-as-for-table-alias`](require-as-for-table-alias.md): Requires `AS` for table/derived-table aliases for consistent, scan-friendly FROM/JOIN clauses.
- [`meaningful-alias`](meaningful-alias.md): Warns on single-character table aliases in multi-table queries; short aliases can obscure meaning and make reviews harder.

## Control Flow Safety

- [`require-begin-end-for-while`](require-begin-end-for-while.md): Enforces `BEGIN/END` for every `WHILE` body to avoid accidental single-statement loops when code is edited.
- [`require-begin-end-for-if-with-controlflow-exception`](require-begin-end-for-if-with-controlflow-exception.md): Enforces `BEGIN/END` for `IF`/`ELSE` blocks, while allowing a single control-flow statement (e.g., `RETURN`) without a block.

## Query Structure & Clarity

- [`disallow-select-star`](disallow-select-star.md): Disallows `SELECT *` / `t.*`; explicit column lists improve readability and prevent accidental breaking changes when schemas evolve.
- [`qualified-select-columns`](qualified-select-columns.md): Requires qualifying columns in SELECT lists when multiple tables are referenced; prevents subtle "wrong table" mistakes when column names overlap.
- [`require-qualified-columns-everywhere`](require-qualified-columns-everywhere.md): Requires column qualification in `WHERE` / `JOIN` / `ORDER BY` when multiple tables are referenced; stricter than `qualified-select-columns`.
- [`require-explicit-join-type`](require-explicit-join-type.md): Disallows ambiguous JOIN shorthand; makes JOIN semantics explicit and consistent across a codebase.
- [`require-parentheses-for-mixed-and-or`](require-parentheses-for-mixed-and-or.md): Requires parentheses when mixing `AND` and `OR`; avoids relying on operator precedence and reduces logic bugs.
- [`require-column-list-for-insert-select`](require-column-list-for-insert-select.md): Requires target column list for `INSERT ... SELECT`; protects against schema drift and column order mistakes.
- [`require-column-list-for-insert-values`](require-column-list-for-insert-values.md): Requires target column list for `INSERT ... VALUES`; makes intent explicit and safer across schema changes.
- [`order-by-in-subquery`](order-by-in-subquery.md): Disallows invalid `ORDER BY` in subqueries unless paired with `TOP`, `OFFSET`, `FOR XML`, or `FOR JSON` (SQL Server error Msg 1033).
- [`forbid-top-100-percent-order-by`](forbid-top-100-percent-order-by.md): Forbids `TOP 100 PERCENT ORDER BY`; it is redundant and often ignored by the optimizer.

## Schema Design

- [`require-primary-key-or-unique-constraint`](require-primary-key-or-unique-constraint.md): Requires PRIMARY KEY or UNIQUE constraints for user tables; helps enforce correctness and supports indexing/relational integrity.
- [`avoid-heap-table`](avoid-heap-table.md): Warns when tables are created as heaps (no clustered index); heaps can lead to unpredictable performance and maintenance costs.
- [`require-ms-description-for-table-definition-file`](require-ms-description-for-table-definition-file.md): Ensures table definition files include an `MS_Description` extended property so schema intent is captured alongside DDL.
- [`disallow-select-into`](disallow-select-into.md): Warns on `SELECT ... INTO`; it implicitly creates schema and can produce fragile, environment-dependent results.

## Performance & Correctness

- [`top-without-order-by`](top-without-order-by.md): Warns when `TOP` is used without `ORDER BY`; results can be non-deterministic across runs/plans.
- [`avoid-top-in-dml`](avoid-top-in-dml.md): Disallows `TOP` in UPDATE/DELETE; it is frequently non-deterministic and easy to misuse without a carefully designed ordering strategy.
- [`avoid-implicit-conversion-in-predicate`](avoid-implicit-conversion-in-predicate.md): Warns when predicates apply conversions to columns; this commonly forces scans and can change comparison semantics.
- [`avoid-null-comparison`](avoid-null-comparison.md): Disallows `= NULL` / `<> NULL`; SQL three-valued logic makes these always evaluate to UNKNOWN - use `IS NULL` / `IS NOT NULL`.
- [`avoid-ambiguous-datetime-literal`](avoid-ambiguous-datetime-literal.md): Disallows slash-delimited date literals; they depend on language/locale and can silently change meaning - prefer ISO 8601.
- [`avoid-atat-identity`](avoid-atat-identity.md): Disallows `@@IDENTITY`; it can return values from triggers - prefer `SCOPE_IDENTITY()` or `OUTPUT`.

## Security

- [`avoid-exec-dynamic-sql`](avoid-exec-dynamic-sql.md): Warns on dynamic SQL executed via `EXEC`; prefer `sp_executesql` with parameters to reduce injection risk and improve plan reuse.
- [`avoid-merge`](avoid-merge.md): Prohibits `MERGE`; due to historical bugs/edge cases and complexity, prefer separate `INSERT`/`UPDATE`/`DELETE` with clear predicates.

## Data Access & Isolation

- [`avoid-nolock`](avoid-nolock.md): Warns on `WITH (NOLOCK)` / `READ UNCOMMITTED`; dirty reads can return inconsistent or incorrect results. *(Note: `avoid-nolock-or-read-uncommitted` has been deprecated in favor of this rule.)*

## Transaction Safety

- [`require-try-catch-for-transaction`](require-try-catch-for-transaction.md): Requires `TRY/CATCH` around explicit transactions to ensure errors trigger rollback and cleanup consistently.
- [`require-xact-abort-on`](require-xact-abort-on.md): Requires `SET XACT_ABORT ON` with explicit transactions to ensure runtime errors reliably abort and roll back work.

## Functions & Built-in Utilities

- [`prefer-coalesce-over-nested-isnull`](prefer-coalesce-over-nested-isnull.md): Detects nested `ISNULL` and recommends `COALESCE`; reduces nesting and aligns with standard SQL behavior.
- [`avoid-magic-convert-style-for-datetime`](avoid-magic-convert-style-for-datetime.md): Warns on datetime `CONVERT` style numbers (magic numbers); encourages clearer, safer formatting patterns.
- [`prefer-concat-over-plus`](prefer-concat-over-plus.md): Recommends `CONCAT()` when `+` concatenation uses `ISNULL`/`COALESCE`; avoids subtle `NULL` propagation. For stricter checking including type conversions, see the next rule.
- [`prefer-concat-over-plus-when-nullable-or-convert`](prefer-concat-over-plus-when-nullable-or-convert.md): Stricter variant that also detects `CAST`/`CONVERT` in concatenations; enable instead of `prefer-concat-over-plus` for comprehensive coverage (SQL Server 2012+).
- [`prefer-concat-ws`](prefer-concat-ws.md): Recommends `CONCAT_WS()` when concatenation repeats the same separator literal; improves readability and reduces duplication (SQL Server 2017+).
- [`prefer-trim-over-ltrim-rtrim`](prefer-trim-over-ltrim-rtrim.md): Recommends `TRIM(x)` instead of `LTRIM(RTRIM(x))`; clearer and less error-prone (SQL Server 2017+).
- [`prefer-string-agg-over-stuff`](prefer-string-agg-over-stuff.md): Recommends `STRING_AGG()` over `STUFF(... FOR XML PATH('') ...)`; simpler and typically faster/safer (SQL Server 2017+).
- [`prefer-try-convert-patterns`](prefer-try-convert-patterns.md): Recommends `TRY_CONVERT`/`TRY_CAST` over `CASE` + `ISNUMERIC`/`ISDATE`; fewer false positives and clearer intent.
- [`prefer-json-functions`](prefer-json-functions.md): Encourages built-in JSON features (`OPENJSON`, `JSON_VALUE`, `FOR JSON`, etc.) over manual string parsing/building (SQL Server 2016+).
