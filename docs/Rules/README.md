# TsqlRefine Rules

This document provides an overview of all built-in rules in TsqlRefine. TsqlRefine includes 89 rules covering various aspects of T-SQL code quality, from correctness and safety to performance and style.

## Table of Contents

- [Overview](#overview)
- [Rule Categories](#rule-categories)
- [Individual Rule Documentation](#individual-rule-documentation)
- [Rule Reference](#rule-reference)
- [Configuration](#configuration)

## Overview

TsqlRefine provides a comprehensive set of rules to enforce T-SQL best practices and detect potential issues in SQL Server code. Each rule has:

- **Rule ID**: Unique identifier (e.g., `avoid-select-star`)
- **Description**: What the rule checks for
- **Category**: Classification of the rule type
- **Default Severity**: Error, Warning, or Information
- **Fixable**: Whether the rule supports auto-fixing

## Rule Categories

TsqlRefine organizes rules into the following categories:

| Category | Description |
|----------|-------------|
| **Correctness** | Detects code that may produce incorrect results or runtime errors |
| **Safety** | Prevents destructive or dangerous operations |
| **Security** | Identifies security vulnerabilities like SQL injection |
| **Performance** | Flags patterns that can cause performance issues |
| **Style** | Maintains code formatting and consistency |
| **Transactions** | Ensures proper transaction handling and session settings |
| **Schema** | Enforces database schema best practices |
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

- Rule statistics (total counts, severity distribution)
- All rules listed by category
- All rules listed by severity
- List of fixable rules with auto-fix support

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
