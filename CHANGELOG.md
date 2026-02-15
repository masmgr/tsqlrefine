# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.1] - 2026-02-15

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

[Unreleased]: https://github.com/masmgr/tsqlrefine/compare/v0.5.1...HEAD
[0.5.1]: https://github.com/masmgr/tsqlrefine/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/masmgr/tsqlrefine/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/masmgr/tsqlrefine/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/masmgr/tsqlrefine/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/masmgr/tsqlrefine/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/masmgr/tsqlrefine/releases/tag/v0.1.0
