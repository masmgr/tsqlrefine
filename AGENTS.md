# AGENTS.md (tsqlrefine)

This repository contains `tsqlrefine`, a .NET CLI tool that provides **linting / static analysis / formatting / fixing** for SQL Server 2012 and later T-SQL, along with its libraries, rules, formatter, schema analysis, plugin SDK, and plugin host.

Follow these principles during development:

- Prefer the ScriptDOM AST over manually scanning token sequences for syntax and semantic analysis.
- Reuse established rule implementation patterns and shared utilities under `Helpers/`.
- Use shared helpers for autofixes, and do not rewrite strings, comments, or unrelated ranges.
- Add or update tests whenever behavior changes.

## Repository Map

- CLI entry point: `src/TsqlRefine.Cli/`
- Shared engine and models: `src/TsqlRefine.Core/`
- Built-in rules: `src/TsqlRefine.Rules/`
- Formatter: `src/TsqlRefine.Formatting/`
- Schema models and name resolution: `src/TsqlRefine.Schema/`
- SQL Server schema extraction: `src/TsqlRefine.Schema.SqlServer/`
- Plugin SDK and host: `src/TsqlRefine.PluginSdk/`, `src/TsqlRefine.PluginHost/`
- Debug and investigation utility (not in the solution): `src/TsqlRefine.DebugTool/`
- Tests: `tests/`
- QA tests and corpus: `tests/TsqlRefine.Qa.Tests/`, `tests/corpus/`
- Samples: `samples/`
- Preset rulesets: `rulesets/`
- JSON Schemas: `schemas/`
- Documentation: `docs/`
- Per-rule documentation: `docs/Rules/`
- Development scripts and utilities: `scripts/` (`*.ps1`) and `tools/`

The primary dependency flow is:

`Cli` → `Core` / `Formatting` / `PluginHost` / `Rules` / `Schema.SqlServer` → `Schema` → `PluginSdk`

`PluginSdk` is a public API consumed by external plugins. Avoid breaking changes, and check the API version, compatibility, and XML documentation impact whenever it changes.

## Development Environment

- `global.json` pins the .NET SDK to `10.0.102`. Use `dotnet --info` to verify that the matching SDK is installed.
- All projects target `net10.0` (`src/*/*.csproj`).
- Dependencies use Central Package Management through `Directory.Packages.props`.

## Development Workflow

1. Inspect the existing implementation and tests closest to the change first.
2. When moving or renaming files, update namespaces, `using` directives, and project references in both production and test projects.
3. Build and run the tests closest to the change first, then run the full solution test suite.
4. Do not guess expected diagnostic counts or locations in tests; verify them against actual rule behavior.

Even when asked to commit, do not commit with failing tests. Report the failures instead.

## Common Commands

```powershell
# build / test
dotnet build src/TsqlRefine.sln -c Release
dotnet test  src/TsqlRefine.sln -c Release

# run the CLI locally (example)
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json

# format (path inputs are written back automatically; stdin is written to stdout)
dotnet run --project src/TsqlRefine.Cli -c Release -- format path\to\file.sql
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin

# fix (path inputs are written back automatically; --output json is a dry run)
dotnet run --project src/TsqlRefine.Cli -c Release -- fix path\to\dir
dotnet run --project src/TsqlRefine.Cli -c Release -- fix --output json path\to\file.sql

# init / list / config
dotnet run --project src/TsqlRefine.Cli -c Release -- init
dotnet run --project src/TsqlRefine.Cli -c Release -- print-config --output json
dotnet run --project src/TsqlRefine.Cli -c Release -- print-format-config --show-sources
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules
dotnet run --project src/TsqlRefine.Cli -c Release -- list-plugins --verbose

# coverage
.\scripts\run-coverage.ps1

# schema
dotnet run --project src/TsqlRefine.Cli -c Release -- schema snapshot --connection-string "Server=...;Database=..." --output schema.json
dotnet run --project src/TsqlRefine.Cli -c Release -- schema collect-relations --output relations.json path\to\sql
dotnet run --project src/TsqlRefine.Cli -c Release -- schema build --connection-string "Server=...;Database=..." --output-dir .tsqlrefine path\to\sql
```

## Coding Conventions

- `Directory.Build.props` enables nullable reference types, implicit usings, and .NET analyzers. Do not bypass analyzer warnings merely to make a change pass.
- `.editorconfig` requires:
  - LF line endings and UTF-8 encoding
  - 4-space indentation for C# and System-first `using` sorting
  - File-scoped namespaces
- Write source-code strings, including messages, and comments in English. Document the reason in the PR if an exception is necessary.
- Prefer `FrozenSet` / `FrozenDictionary` for static, read-only lookup collections when appropriate.
- Consider `StringBuilder` for string concatenation in hot paths, and cache repeated expensive computations.

## Change Checklist

- **When changing CLI options or output JSON**: Update `docs/cli.md`, `README.md`, and tests in `tests/TsqlRefine.Cli.Tests/`.
- **When changing CLI JSON output models**: Update the corresponding `schemas/*-result.schema.json`.
- **When changing configuration files (config/ruleset/plugins)**: Update `schemas/` (`tsqlrefine.schema.json` / `ruleset.schema.json`), `docs/configuration.md`, and `samples/` when applicable.
- **When adding or changing a rule**:
  - Rule implementation: `src/TsqlRefine.Rules/Rules/`
  - Rule lists and presets: `rulesets/` when applicable
  - Tests: `tests/TsqlRefine.Rules.Tests/` and/or `tests/TsqlRefine.Core.Tests/`
  - Sample SQL: `samples/sql/` for new rules or when otherwise applicable
- **When updating per-rule documentation**: Update `docs/Rules/` and `docs/Rules/REFERENCE.md`. `docs/Rules/README.md` is maintained manually and normally should not be edited.
- **When changing plugin loading**: `src/TsqlRefine.PluginHost/` crosses an AssemblyLoadContext boundary, so pay close attention to dependency resolution and duplicate assembly loading.

## Configuration and Documentation

- Standard configuration file: `tsqlrefine.json`
- Built-in presets: `recommended` (default), `strict`, `strict-logic`, `pragmatic`, `security-only`
- Custom rulesets: Set `ruleset` to a name or file path. Short names are resolved from `.tsqlrefine/rulesets/`.
- Configuration details: `docs/configuration.md`
- CLI specification: `docs/cli.md`
- Formatting specification: `docs/formatting.md`
- Plugin API: `docs/plugin-api.md`
- Rule reference: `docs/Rules/REFERENCE.md`
- Quality assurance: `docs/quality-assurance.md`

When applicable, also consult the detailed patterns for each implementation area under `.claude/rules/`:

- General: `.claude/rules/project-conventions.md`
- Rules: `.claude/rules/rules-development.md`
- Formatting: `.claude/rules/formatting-development.md`
- CLI: `.claude/rules/cli-development.md`
- Core / Plugin SDK: `.claude/rules/core-development.md`
- Plugin Host: `.claude/rules/plugin-development.md`
- Tests: `.claude/rules/testing-patterns.md`

## Important Implementation Behavior

- The CLI requires a subcommand; omitting one does not default to `lint`.
- `format` and `fix` automatically write changes back when given paths. With stdin, they write to stdout.
- `fix --output json` reports diagnostics only and does not write changes back to files.
