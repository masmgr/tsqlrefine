# TsqlRefine Rules

This document provides an overview of all built-in rules in TsqlRefine. TsqlRefine includes **130 rules** covering various aspects of T-SQL code quality, from correctness and safety to performance and style.

## Table of Contents

- [Overview](#overview)
- [Importance Tiers](#importance-tiers)
- [Rule Categories](#rule-categories)
- [Individual Rule Documentation](#individual-rule-documentation)
- [Rule Reference](#rule-reference)
- [Configuration](#configuration)

## Overview

TsqlRefine provides a comprehensive set of rules to enforce T-SQL best practices and detect potential issues in SQL Server code. Each rule has:

- **Rule ID**: Unique identifier (e.g., `avoid-select-star`)
- **Description**: What the rule checks for
- **Category**: Classification of the rule type
- **Importance Tier**: Criticality level based on preset membership
- **Default Severity**: Error, Warning, or Information
- **Fixable**: Whether the rule supports auto-fixing

## Importance Tiers

Rules are organized into five importance tiers based on which preset first includes them. Each higher preset is a strict superset of the one below:

```
security-only ⊂ pragmatic ⊂ recommended ⊂ strict-logic ⊂ strict
```

| Tier | Preset | Rules | Description |
|------|--------|-------|-------------|
| **Critical** | security-only | 14 | Security vulnerabilities and critical safety issues |
| **Essential** | pragmatic | 29 | Production-ready minimum for correctness and runtime error prevention |
| **Recommended** | recommended | 44 | Balanced production use with semantic analysis and best practices |
| **Thorough** | strict-logic | 20 | Comprehensive correctness, performance, and schema checks |
| **Cosmetic** | strict | 23 | Style consistency, formatting, and naming conventions |

See [REFERENCE.md](REFERENCE.md#importance-tiers) for the complete tier breakdown with all rules listed.

## Rule Categories

TsqlRefine organizes rules into the following categories:

| Category | Description |
|----------|-------------|
| **Security** | Identifies security vulnerabilities like SQL injection |
| **Safety** | Prevents destructive or dangerous operations |
| **Correctness** | Detects code that may produce incorrect results or runtime errors |
| **Performance** | Flags patterns that can cause performance issues |
| **Transactions** | Ensures proper transaction handling and session settings |
| **Schema** | Enforces database schema best practices |
| **Style** | Maintains code formatting and consistency |
| **Debug** | Controls debug and output statements |

## Individual Rule Documentation

For detailed information about each rule, including examples and configuration options, see the individual rule documentation files in the category subdirectories:

- [Correctness Rules](correctness/)
- [Safety Rules](safety/)
- [Security Rules](security/)
- [Performance Rules](performance/)
- [Style Rules](style/)
- [Transactions Rules](transactions/)
- [Schema Rules](schema/)
- [Debug Rules](debug/)

## Rule Reference

For complete rule listings, statistics, and cross-reference tables, see [REFERENCE.md](REFERENCE.md). It includes:

- Rule statistics (total counts, tier breakdown, severity distribution)
- All rules listed by importance tier (primary classification)
- All rules listed by severity
- List of fixable rules with auto-fix support

## Configuration

Rules can be configured in `tsqlrefine.json` or via ruleset files. See [Configuration Documentation](../configuration.md) for details.

### Configuring Rules

To disable or change the severity of specific rules, use a ruleset file or the `rules` property in `tsqlrefine.json`.

**Ruleset file** (array format):

```json
{
  "rules": [
    { "id": "avoid-select-star", "severity": "none" },
    { "id": "dml-without-where", "severity": "error" }
  ]
}
```

**tsqlrefine.json** (object format, overrides preset/ruleset):

```json
{
  "preset": "recommended",
  "rules": {
    "avoid-select-star": "none",
    "dml-without-where": "error"
  }
}
```

Each rule's `severity` controls both enablement and severity level:

| Value | Effect |
|-------|--------|
| `"error"` | Enable with Error severity |
| `"warning"` | Enable with Warning severity |
| `"info"` | Enable with Information severity |
| `"inherit"` | Enable with the rule's default severity |
| `"none"` | Disable the rule |

When `severity` is omitted in a ruleset file, it defaults to `"inherit"`.

### Preset Rulesets

TsqlRefine provides five preset rulesets:

| Preset | Rules | Cumulative | Use Case |
|--------|-------|------------|----------|
| **security-only** | 14 | 14 | Security vulnerabilities and critical safety only |
| **pragmatic** | 43 | 43 | Production-ready minimum for legacy codebases |
| **recommended** | 87 | 87 | Balanced production use with semantic analysis (default) |
| **strict-logic** | 107 | 107 | Comprehensive correctness without cosmetic style rules |
| **strict** | 130 | 130 | Maximum enforcement including all style/cosmetic rules |

```powershell
# Use recommended preset (default)
dotnet run --project src/TsqlRefine.Cli -- lint file.sql

# Use strict-logic for comprehensive checking without style noise
dotnet run --project src/TsqlRefine.Cli -- lint --preset strict-logic file.sql

# Use strict for maximum enforcement
dotnet run --project src/TsqlRefine.Cli -- lint --preset strict file.sql
```

## Contributing

To add a new rule, see [CLAUDE.md](../../CLAUDE.md#adding-a-new-built-in-rule) for implementation guidelines.
