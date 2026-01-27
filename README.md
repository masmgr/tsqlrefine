# tsqlrefine

T-SQL lint / check / format / fix for SQL Server.

This repository contains the `.NET` CLI tool `tsqlrefine`, built-in rules, a formatter, and a plugin host/SDK for external rule providers.

Note: This project is currently in early development (pre-1.0). Breaking changes are expected.

## Quickstart

Build and test:

```powershell
dotnet build src/TsqlRefine.sln -c Release
dotnet test  src/TsqlRefine.sln -c Release
```

Run the CLI from source:

```powershell
# Lint from stdin (JSON output)
"select * from t;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json

# Lint files/directories
dotnet run --project src/TsqlRefine.Cli -c Release -- lint path\to\file.sql path\to\dir

# Format in-place (writes files)
dotnet run --project src/TsqlRefine.Cli -c Release -- format --write path\to\dir

# Show a diff instead of writing
dotnet run --project src/TsqlRefine.Cli -c Release -- format --diff path\to\file.sql
```

## Configuration

Generate default config files in the current directory:

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- init
```

This creates:

- `tsqlrefine.json` (tool configuration; see schema in `schemas/tsqlrefine.schema.json`)
- `tsqlrefine.ignore` (one glob per line; lines starting with `#` are comments)

Common options:

- `--config <path>`: override config discovery
- `--ignorelist <path>`: override ignore list discovery
- `--ruleset <path>`: override the ruleset file from config
- `--preset <recommended|strict|pragmatic|security-only>`: use a preset ruleset from `rulesets/`
- `--output <text|json>`: choose output format (for `lint`/`check`)

## Rules and plugins

- List built-in and loaded rules: `tsqlrefine list-rules`
- List loaded plugins: `tsqlrefine list-plugins`
- Preset rulesets live in `rulesets/`
- External rules can be loaded via `plugins` in `tsqlrefine.json` (see `docs/plugin-api.md`)

## Docs

Project docs live under `docs/` (currently written in Japanese):

- Requirements: `docs/requirements.md`
- Rules and presets: `docs/rules.md`
- Task list / roadmap: `docs/task-list.md`
- CLI spec (I/O, JSON, exit codes): `docs/cli.md`
- Plugin API (minimum contract: Rule): `docs/plugin-api.md`
- Project structure: `docs/project-structure.md`
