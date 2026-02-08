# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/imasa/tsqlrefine/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/imasa/tsqlrefine/releases/tag/v0.1.0
