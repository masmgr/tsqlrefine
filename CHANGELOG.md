# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- `avoid-top-without-order-by-in-select-into` no longer reports constant `TOP 0`, uses table-neutral wording, and defaults to Warning
- `avoid-named-constraint-in-temp-table` now defaults to Warning
- `semantic-data-type-length` no longer offers arbitrary length autofixes; lengths must be selected explicitly
- `semantic-unicode-string` now reports only national (`N'...'`) literals converted to `VARCHAR`/`CHAR`; non-national literal policy remains with `prefer-unicode-string-literals`

### Fixed

- Empty procedure and trigger bodies no longer crash control-flow and SQL metrics rules
- Rule implementation exceptions use the dedicated `rule-exception` analysis-failure diagnostic instead of masquerading as rule violations
- `dynamic-sql-taint` now preserves trust through simple and searched `CASE` expressions

## [2.0.0] - 2026-07-20

### Added

- `analyze impact` / `analyze graph` CLI commands: transitive object-catalog impact analysis and dependency-graph export (JSON/DOT)
- `schema diff` CLI command for breaking schema drift detection with optional transitive object-catalog impact analysis
- `report` CLI command: diagnostic aggregations and top-complexity SQL metrics in JSON or HTML
- `schema collect-objects` CLI command and SQL object catalog collection (procedure/function/view signatures and static/dynamic references)
- `baseline create` / `baseline trim` CLI commands and `--baseline`/`--root`/`--show-suppressed` lint options for diagnostic baselines
- `--output sarif` lint output (SARIF 2.1.0) for GitHub Code Scanning and other SARIF consumers
- `--changed-only` / `--base-ref` / `--changed-lines-from` lint options for Git-diff-scoped linting
- `TSQLREFINE_CONNECTION_STRING` environment variable support for schema commands
- Typed per-rule options (`IRuleOptions` / `IRuleOptionsDescriptorProvider`) validated against declared descriptors, exposed to rules without leaking JSON types into the PluginSdk
- Control-flow graph and data-flow analysis framework (including trigger bodies) powering path-sensitive rules
- `dynamic-sql-taint` rule: tracks tainted values through variable assignments into `EXEC`/`sp_executesql` sinks across control-flow paths
- `cursor-not-deallocated-on-path`, `inconsistent-result-set`, `unreachable-statement`, `unused-variable`, `variable-used-before-assignment`, `transaction-not-closed-on-path` path-sensitive rules
- `exec-output-not-captured`, `exec-parameter-count-mismatch`, `exec-parameter-name-mismatch`, `exec-parameter-type-mismatch` rules validating `EXEC` calls against the object catalog
- `circular-object-reference`, `unreferenced-object`, `unresolved-procedure-reference` rules using the object dependency graph
- `deep-view-nesting` rule
- Configurable SQL metrics thresholds: `max-cyclomatic-complexity`, `max-joins-per-query`, `max-nesting-depth`, `max-parameter-count`, `max-statement-count` rules
- `require-semicolon-before-throw` rule: detect `THROW` statements missing a preceding semicolon
- `avoid-hardcoded-password` rule: detect password literals in SQL source
- `multi-row-update-from` rule: detect non-deterministic `UPDATE ... FROM` that joins multiple rows to a single target row
- `len-for-emptiness-check` rule: detect `LEN(col) = 0` / `LEN(col) > 0` patterns that should use `= ''` / `<> ''`
- `string-agg-nvarchar-max` rule: detect `STRING_AGG` calls missing `NVARCHAR(MAX)` cast on the separator
- `join-column-deviation` rule: detect JOIN column patterns that deviate from learned relation profiles
- `update-join-cardinality-mismatch` rule: detect non-deterministic `UPDATE ... FROM ... JOIN` patterns
- `delete-column-not-in-table` rule: detect column references in `DELETE ... FROM` that don't exist in the target table
- `index-column-not-in-table` rule: detect columns in `CREATE INDEX` that don't exist in the target table
- `join-foreign-key-mismatch` rule: detect `JOIN` conditions that don't match declared foreign-key relationships
- `schema-aware-implicit-conversion` rule: detect implicit type conversions using schema column types
- Schema-aware reference validation rules for column/table reference accuracy
- `ISchemaContext` interface unifying schema and relation-deviation access (`ISchemaProvider` + `RelationDeviations`)
- `SchemaContext` adapter class wrapping `ISchemaProvider` and optional `IRelationDeviationProvider`
- `schema build` CLI command: generate `schema.json` + `relations.json` + `objects.json` in one step via `--connection-string` + `--output-dir`
- `schema collect-relations` CLI command: extract JOIN patterns from SQL files
- `SchemaConfig.Path` shorthand in `tsqlrefine.json`: derives `schema.json` and `relations.json` from a single directory path
- `schema.relationsProfilePath` config option for custom relation profile location
- ER relationship query API on `ISchemaProvider`
- Per-query caching and `TryResolve` helper in schema rules for repeated lookups
- Plugin API bumped to v5 (v4 added object catalog access via `RuleContext.ObjectCatalog`; v5 added typed rule options)
- `TsqlRefine.Schema.SqlServer` project for SQL Server schema snapshot extraction

### Changed

- Multi-file analysis and input reading parallelized
- `avoid-deprecated-types`: TIMESTAMP type detection strengthened
- `union-type-mismatch` and `left-join-filtered-by-where` rules enhanced with schema awareness
- `ConfigLoader` now exposes unified `LoadSchemaContext()` replacing separate `LoadSchema` + `LoadRelationDeviations` calls
- `NameResolver` and `RelationDeviationProvider` use `FrozenDictionary` for hot-path lookups
- `QualifierLookupKeyBuilder` extracted as shared helper from duplicate schema rule implementations
- Schema lookup helpers unified via shared base to eliminate duplication across rules
- Formatter: removed unused AST operator path, fixed protected-region edge cases
- Multi-row update join diagnostic message clarified

### Fixed

- `dml-without-where`: alias-targeted `UPDATE`/`DELETE` (e.g. `DELETE u FROM Users AS u`) is no longer exempted, since an alias alone does not limit affected rows
- `unresolved-procedure-reference`: system stored/extended procedure references (`sp_*`/`xp_*`) no longer misreported as missing from the object catalog
- `exec-parameter-type-mismatch`: string arguments with unknown length now correctly flagged against fixed-length parameters instead of being skipped
- Control-flow analysis: trigger bodies are now included in CFG-based rules; dynamic SQL taint tracking handles indirect variable writes (e.g. `EXEC ... INTO`, `SELECT` assignments) and constant-folds adjacent string-literal concatenations more accurately
- `semantic-schema-qualify`: false positive on CTE references
- `avoid-not-in-with-null`: suppressed when schema proves column is NOT NULL
- `top-without-order-by`: suppressed when `WHERE` clause filters on a unique column
- `unresolved-table-reference`: CTE references and DML alias targets now correctly skipped
- `update-column-not-in-table`: alias targets now resolved before schema lookup
- Schema-qualified column references now resolved in column and implicit-conversion rules
- Schema-aware suppression accuracy improved across three rules
- Schema rule resolution edge cases for nested JOIN alias matching
- Complex rule edge cases (multiple scenarios across schema and semantic rules)
- CLI encoding issues on Windows non-UTF-8 consoles
- CLI plugin validation errors on startup
- CLI ignore-list handling edge cases
- Plugin resources now properly disposed; added defensive null checks (CA1001/CA2000)
- `ScriptDomTokenizer`: double-parse avoided by reusing fragment token stream

### Dependencies

- `SqlScriptDom` bumped to 180.18.1
- `actions/checkout` bumped from 6.0.2 to 6.0.3
- `actions/setup-dotnet` bumped from 5.2.0 to 5.3.0
- `actions/upload-artifact` bumped from 7.0.0 to 7.0.1
- `softprops/action-gh-release` bumped from 2.6.1 to 3.0.0

## [1.2.0] - 2026-03-01

### Fixed

- `insert-column-count-mismatch`: diagnostic highlight narrowed from entire `InsertStatement` to first token range
- `prefer-try-convert-patterns`: diagnostic highlight narrowed from entire `SearchedCaseExpression` to `CASE` keyword
- `insert-select-column-name-mismatch`: diagnostic highlight narrowed from entire `InsertStatement` to first mismatched INSERT column
- `avoid-between-for-datetime-range`: diagnostic highlight narrowed from entire ternary expression to `BETWEEN` keyword

## [1.1.0] - 2026-02-18

### Added

- Multi-target framework support for .NET 8, 9, and 10 — CLI tool can now be installed on .NET 8+ environments

### Fixed

- Plugin warning no longer shown when `--quiet` is specified

## [1.0.1] - 2026-02-17

No user-facing changes.

## [1.0.0] - 2026-02-16

First stable release.

### Added

- Core linting engine with 130+ built-in rules covering security, performance, correctness, style, schema, and transactions
- Auto-fix capability for fixable rules (e.g., keyword casing, NULL comparison operators, EXEC to sp_executesql)
- SQL formatter with keyword uppercasing, indentation, and whitespace normalization
- CLI commands: `lint`, `fix`, `format`, `init`, `list-rules`, `list-plugins`, `print-config`
- Plugin system for custom rules with .NET plugin loading and API version verification
- Configuration via `tsqlrefine.json`, `.tsqlrefine/` directory discovery, and `.editorconfig` integration
- Preset rulesets: `recommended`, `strict`, `strict-logic`, `pragmatic`, `security-only`
- Named ruleset resolution from `.tsqlrefine/rulesets/` directory
- Per-rule severity configuration (`error`, `warning`, `info`, `inherit`, `none`)
- JSON output format for CI/CD integration
- Inline disable comments (`-- tsqlrefine-disable-next-line rule-id`) with optional reason text
- Security hardening: `--allow-plugins` opt-in flag, `--max-file-size` option, plugin path validation
- Documentation URI for each rule for editor integration
- CI integration guide with GitHub Actions, Azure Pipelines, and GitLab CI examples
- Editor integration guide with VS Code tasks, pre-commit hooks, and JetBrains setup
- TsqlRefine.PluginSdk published as NuGet package

## [0.6.0] - 2026-02-16

### Fixed

- `aggregate-in-where-clause`: false positive on scalar subquery containing WHERE clause
- `order-by-in-subquery`: false positive on INSERT...SELECT...ORDER BY

## [0.5.1] - 2026-02-15

### Changed

- Bump GitHub Actions dependencies: actions/checkout v6.0.2, actions/setup-dotnet v5.1.0, actions/upload-artifact v6.0.0, softprops/action-gh-release v2.5.0

## [0.5.0] - 2026-02-15

### Added

- Security hardening: `--allow-plugins` opt-in flag, `--max-file-size` option (default 10 MB), plugin API version verification via assembly attribute, plugin path validation
- `SECURITY.md` vulnerability reporting policy
- Dependabot configuration for NuGet and GitHub Actions dependency updates
- Vulnerable package scanning in CI workflow
- SHA256 checksums for GitHub Release artifacts
- GitHub Actions pinned to commit SHAs for supply chain security
- `--enabled-only` option for `list-rules` command to hide disabled rules
- `--verbose` option for `fix` command to display execution time
- `--preset` / `--ruleset` mutual exclusion validation
- `--verbose` / `--quiet` mutual exclusion validation
- Plugin search paths for filename-only plugin references
- CI integration guide with GitHub Actions, Azure Pipelines, and GitLab CI examples
- Editor integration guide with VS Code tasks, pre-commit hooks, and JetBrains setup
- CI and editor integration sample files
- Named ruleset resolution from `.tsqlrefine/rulesets/` directory, legacy file warnings, and config priority tests
- NuGet package link in README Installation section
- VS Code extension references in documentation

### Changed

- Renamed 27 rule IDs for kebab-case naming consistency
- Warn when `--rule` overrides `--preset` or `--ruleset` on `fix` command
- Removed `-g` short alias from `--ignorelist`
- Removed unused `--output` option from `format` command
- Removed unused `--output` option from `print-config` command
- `--max-file-size` rejects invalid values instead of silent fallback
- Suppress stack traces and probe paths in non-verbose plugin output

### Fixed

- `avoid-not-in-with-null` highlight narrowed to NOT IN keyword
- JSON schema: added missing `compatLevel` values (130, 140) and corrected casing defaults
- `FormattingConfig` default casing for schema/table/column corrected to `None`
- CLI docs: added `--allow-plugins`, `--max-file-size`, fixed rule ID format
- Plugin API docs: corrected rules link and removed outdated phrasing
- Configuration docs: added missing `compatLevel` values 130 and 140
- Formatting docs: fixed CompatLevel range from 100–170 to 100–160
- Samples README: fixed paths, rule counts, output examples, and URLs

## [0.4.0] - 2026-02-14

### Added

- New correctness rules: `group-by-column-mismatch`, `having-column-mismatch`, `aggregate-in-where-clause`
- New performance rules: `avoid-scalar-udf-in-query`, `avoid-correlated-subquery`, `avoid-cross-column-or`
- New style rules: `require-alias-as-keyword`, `require-semicolon`, `prefer-ansi-join`
- 4 SET preamble rules for session environment consistency: `require-set-nocount`, `require-set-xact-abort`, `require-set-ansi-nulls`, `require-set-quoted-identifier`
- 7 additional lint rules covering correctness, performance, security, schema, and transactions
- Autofix for `avoid-null-comparison` rule
- IS NOT NULL guard exemption for `prefer-exists-over-in-subquery` rule
- `.tsqlrefine/` directory support for config file discovery
- Source context display for parse errors in text output
- Formatting pipeline enhancements and new options

### Changed

- Preset ruleset composition revised with enforced subset hierarchy
- Pragmatic ruleset tightened to safety and correctness focus
- Plugin rules enabled by default under preset/ruleset whitelist
- Default to `recommended` preset when no ruleset is configured
- Rule docs reorganized by importance tier derived from presets

### Fixed

- Diagnostic spans narrowed to precise keywords for 15+ rules (security, transaction, BEGIN/END, catch, data-compression, require-primary-key, require-ms-description, prefer-exists-over-in-subquery, STUFF, print-statement, and more)
- GROUP BY / HAVING mismatch handling for grouping sets with deduplication
- Aggregate detection gaps in WHERE clause analysis
- Window functions and bracketed identifiers in group/having mismatch rules
- Union/insert-select type and column-name mismatch detection gaps
- Duplicate alias detection extended to recursive queries and DML scopes
- Helper scope handling regressions
- Ruleset null handling and inline-disable rule ID semantics
- `cross-database-transaction` detection for unterminated transactions and JOIN sources
- Heap table and `ms_description` detection improvements
- `avoid-scalar-udf-in-query` limited to query contexts to reduce false positives
- `ban-query-hints` tuned for production-oriented exclusions
- `top-without-order-by` detection in nested queries
- `order-by-in-subquery` handling for CTE and FOR clause
- `prefer-json-functions` false positives reduced
- Trailing-comma formatting around line comments
- GROUP BY expression support in `group-by-column-mismatch` and `having-column-mismatch`
- `qualified-select-columns` edge cases

## [0.3.0] - 2026-02-11

### Added

- Duplicate column detection rules: `duplicate-view-column`, `duplicate-table-function-column`, `duplicate-table-variable-column`, `duplicate-select-column`, `duplicate-insert-column`
- Per-rule severity configuration via `rules` object in `tsqlrefine.json` and ruleset files (`error`, `warning`, `info`, `inherit`, `none`)
- Extended `semantic-undefined-alias` rule to cover MERGE OUTPUT clauses and APPLY arguments
- Documentation URI for each rule in `list-rules --output json` and `codeDescriptionHref` in lint/fix JSON output for editor integration

### Changed

- Renamed semantic rule IDs from `semantic/` prefix to `semantic-` hyphen separator for kebab-case consistency
- Temporary tables excluded from schema-level rules to reduce false positives
- Internal rule architecture migrated to AST-first detection with unified visitor base classes

### Fixed

- Diagnostic ranges narrowed to precise keyword or sub-fragment locations for 18+ rules instead of spanning entire statements
- Formatter regressions in operator spacing, comma handling, and keyword casing logic
- Multi-line protected regions preserved correctly in inline space normalizer
- Preset rulesets resolved from application base directory for NuGet tool distribution
- NuGet source mapping configuration for build reliability

## [0.2.0] - 2026-02-10

### Added

- Schema duplicate detection rules: `duplicate-column-definition`, `duplicate-index-column`, `duplicate-index-definition`, `duplicate-foreign-key-column`
- Positional parameter and column-level MS_Description checks in `require-ms-description` rule
- Reason text support in inline disable comments (`-- tsqlrefine-disable rule-id: reason`)
- `--utf8` global option for console encoding on Windows
- `KeywordSpaceNormalizer` for compound keyword spacing (e.g., `LEFT  OUTER  JOIN` → `LEFT OUTER JOIN`)
- TsqlRefine.PluginSdk published as NuGet package

### Changed

- Refactored formatting casing and token helper structure

### Fixed

- Semantic rule edge cases with expanded regression tests
- Leading comma conversion and nested comment protection
- GitHub repository URL

## [0.1.0] - 2026-02-08

### Added

- Core linting engine with 101 built-in rules covering security, performance, correctness, style, and transactions
- Auto-fix capability for fixable rules (e.g., keyword casing, NULL comparison operators, EXEC to sp_executesql)
- SQL formatter with keyword uppercasing, indentation, and whitespace normalization
- CLI commands: `lint`, `fix`, `format`, `init`, `list-rules`, `list-plugins`
- Plugin system for custom rules with .NET plugin loading
- Configuration via `tsqlrefine.json` and `.editorconfig` integration
- Preset rulesets: `recommended`, `strict`, `strict-logic`, `pragmatic`, `security-only`
- JSON output format for CI/CD integration
- Inline disable comments (`-- tsqlrefine-disable-next-line rule-id`)
- Property-based testing with FsCheck for quality validation
- `--quiet` option to suppress informational output for IDE integration
- Exit codes for programmatic usage (0=success, 1=violations, 2=parse error, 3=config error, 4=fatal error)

[Unreleased]: https://github.com/masmgr/tsqlrefine/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/masmgr/tsqlrefine/compare/v1.2.0...v2.0.0
[1.2.0]: https://github.com/masmgr/tsqlrefine/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/masmgr/tsqlrefine/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/masmgr/tsqlrefine/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/masmgr/tsqlrefine/compare/v0.6.0...v1.0.0
[0.6.0]: https://github.com/masmgr/tsqlrefine/compare/v0.5.1...v0.6.0
[0.5.1]: https://github.com/masmgr/tsqlrefine/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/masmgr/tsqlrefine/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/masmgr/tsqlrefine/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/masmgr/tsqlrefine/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/masmgr/tsqlrefine/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/masmgr/tsqlrefine/releases/tag/v0.1.0
