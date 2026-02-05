# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**tsqlrefine** is a SQL Server/T-SQL linter, static analyzer, and formatter written in C#. It provides:
- Lint and static analysis for T-SQL code (SQL Server 2012+)
- Minimal SQL formatting (keyword casing, whitespace normalization)
- Plugin system for custom rules
- CLI tool and library for integration

Key patterns: rules use ScriptDOM AST (not token-based), helpers live in organized subdirectories under `Helpers/`, and autofix logic uses shared helper classes. When refactoring rules, follow the AST-based pattern used by other rules in the codebase.

## Workflow

After any refactoring or code changes, always run the full test suite (all ~2000 tests) before committing. Never commit with failing tests.

## Refactoring Checklist

When moving/renaming files or reorganizing directories, always update namespaces AND using statements in both the main project and the test project. Build before running tests to catch missing references early.

## Testing

When adding new tests, double-check expected error counts and assertion values against the actual rule behavior. Run the specific new tests first before running the full suite.

## Performance Conventions

Prefer `FrozenSet`/`FrozenDictionary` over `HashSet`/`Dictionary` for static lookup collections. Use `StringBuilder` for string concatenation in hot paths. Cache repeated computations.

## Quick Reference Commands

```powershell
# Build
dotnet build src/TsqlRefine.sln -c Release

# Test
dotnet test src/TsqlRefine.sln -c Release

# Lint SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql

# Format SQL
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# Auto-fix
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql

# Lint from stdin
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin
```

## Architecture Overview

```
src/
├── TsqlRefine.PluginSdk/     # Contracts and interfaces (foundation, zero dependencies)
├── TsqlRefine.Core/          # Analysis engine and tokenizer
├── TsqlRefine.Rules/         # Built-in rules and helper classes
├── TsqlRefine.Formatting/    # SQL formatter
├── TsqlRefine.PluginHost/    # Plugin loading infrastructure
└── TsqlRefine.Cli/           # Command-line interface
```

**Dependency flow**: `Cli` → `Core`/`Formatting`/`PluginHost`/`Rules` → `PluginSdk`

## Configuration

### tsqlrefine.json

```json
{
  "compatLevel": 150,
  "ruleset": "rulesets/recommended.json",
  "plugins": [
    { "path": "plugins/custom.dll", "enabled": true }
  ]
}
```

**Preset rulesets** in `rulesets/`:
- `recommended.json`: Balanced production use (49 rules)
- `strict.json`: Maximum enforcement (86 rules)
- `pragmatic.json`: Production-ready minimum (30 rules)
- `security-only.json`: Security and critical safety (10 rules)

### .editorconfig

Format command respects `.editorconfig` for indentation:
```ini
[*.sql]
indent_style = spaces
indent_size = 4
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no violations) |
| 1 | Rule violations found |
| 2 | Parse error |
| 3 | Config error |
| 4 | Runtime exception |

## Documentation

- [docs/cli.md](docs/cli.md): CLI specification
- [docs/configuration.md](docs/configuration.md): Configuration format
- [docs/formatting.md](docs/formatting.md): Formatting options
- [docs/plugin-api.md](docs/plugin-api.md): Plugin API contract
- [docs/Rules/README.md](docs/Rules/README.md): All 86 built-in rules

## Development Guidelines

Path-specific development patterns are in `.claude/rules/`:

| File | Applies To |
|------|------------|
| [project-conventions.md](.claude/rules/project-conventions.md) | All code (global conventions) |
| [rules-development.md](.claude/rules/rules-development.md) | `src/TsqlRefine.Rules/**` |
| [formatting-development.md](.claude/rules/formatting-development.md) | `src/TsqlRefine.Formatting/**` |
| [cli-development.md](.claude/rules/cli-development.md) | `src/TsqlRefine.Cli/**` |
| [core-development.md](.claude/rules/core-development.md) | `src/TsqlRefine.Core/**`, `src/TsqlRefine.PluginSdk/**` |
| [plugin-development.md](.claude/rules/plugin-development.md) | `src/TsqlRefine.PluginHost/**` |
| [testing-patterns.md](.claude/rules/testing-patterns.md) | `tests/**` |

These rules use YAML frontmatter with `paths` field to automatically load context-specific guidance when working with files in those directories.


Before committing any refactoring changes: 1) run dotnet build to verify no namespace/reference issues, 2) run the full test suite, 3) if any tests fail, fix them and re-run before committing. Pay special attention to test project using statements when files are moved.

Create a detailed todo plan for this refactoring. Include explicit checkpoints: after any file moves, run dotnet build before proceeding. After logic changes, run tests on just the affected test files before running the full suite. Then execute the plan step by step.

I need to refactor these rule files to use the same AST-based pattern: [Rule1.cs, Rule2.cs, Rule3.cs]. Start with Rule1 — analyze it, refactor it, and run tests. Then apply the same patterns to Rule2 and Rule3. Run the full test suite after each rule and commit each separately.

Analyze the rules directory and identify all T-SQL lint rule files still using token-based approaches instead of ScriptDOM AST. For each one, spawn a separate Task that: 1) reads the existing rule and its tests, 2) studies the pattern used in recently refactored AST-based rules (e.g., UndefinedAliasRule.cs, JoinKeywordRule.cs) for consistency, 3) rewrites the rule to use ScriptDOM AST with proper visitor patterns, 4) updates or adds unit tests, 5) runs the full test suite with 'dotnet test' and only commits if all tests pass. Process up to 4 rules in parallel. After all tasks complete, summarize what was refactored, any failures, and remaining candidates.

Perform a systematic edge-case audit of all T-SQL lint rules in the codebase. For each rule: 1) Read the rule implementation and existing tests, 2) Identify categories of T-SQL constructs that could trigger edge cases (CTEs, nested subqueries, CROSS APPLY, PIVOT, window functions, temp tables, dynamic SQL, multi-part names, quoted identifiers), 3) Write 5-10 targeted test cases per rule exercising these edge cases, 4) Run 'dotnet test' to see which new tests fail, 5) For each failure, determine if it's a genuine bug in the rule (not the test), fix the rule, and re-run until all tests pass, 6) Commit passing tests and fixes per-rule with descriptive messages. Track results in a markdown report at docs/EDGE_CASE_AUDIT.md with columns: Rule, Edge Cases Found, Bugs Fixed, Tests Added.

You are a CI repair agent. Run 'dotnet build' and 'dotnet test' for the solution. If either fails: 1) Parse the error output to identify every failing file, line number, and error code, 2) Categorize each error (missing using/namespace, incorrect test assertion, moved file reference, type mismatch, etc.), 3) For namespace/using errors: scan for the correct namespace in the target file and update the reference. For test assertion errors: run the test in isolation, capture actual output, determine if the actual behavior is correct (by reading the rule logic), and update the expected value. For build errors from file moves: use Glob to find the new file location and update all references. 4) After applying fixes, re-run 'dotnet build' and 'dotnet test'. 5) Repeat up to 3 cycles until everything passes. 6) Commit all fixes with message 'fix: resolve build/test failures from [root cause summary]'. Report what was broken and how each issue was resolved.
