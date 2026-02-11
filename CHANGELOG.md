# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/masmgr/tsqlrefine/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/masmgr/tsqlrefine/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/masmgr/tsqlrefine/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/masmgr/tsqlrefine/releases/tag/v0.1.0
