# tsqlrefine

T-SQL lint / check / format / fix for SQL Server.

This repository contains the `.NET` CLI tool `tsqlrefine`, built-in rules, a formatter, and a plugin host/SDK for external rule providers.

Note: This project is currently in early development (pre-1.0). Breaking changes are expected.

## Installation

### As a .NET Global Tool (Recommended)

Install from NuGet.org:

```bash
dotnet tool install --global TsqlRefine
```

Update to the latest version:

```bash
dotnet tool update --global TsqlRefine
```

Uninstall:

```bash
dotnet tool uninstall --global TsqlRefine
```

After installation, the `tsqlrefine` command will be available globally:

```bash
tsqlrefine --version
tsqlrefine --help
```

### As a Local Tool (Project-specific)

For project-specific tool management:

```bash
# Create tool manifest (if not already present)
dotnet new tool-manifest

# Install as a local tool
dotnet tool install TsqlRefine

# Run using dotnet prefix
dotnet tsqlrefine --help
```

### From Source

Clone the repository and build from source:

```bash
git clone https://github.com/imasa/tsqlrefine.git
cd tsqlrefine
dotnet build src/TsqlRefine.sln -c Release
dotnet test src/TsqlRefine.sln -c Release
```

## Quickstart

### Basic Usage

If installed as a global tool:

```bash
# Lint files/directories
tsqlrefine lint path/to/file.sql path/to/dir

# Lint from stdin with JSON output
echo "select * from t;" | tsqlrefine lint --stdin --output json

# Format to stdout
tsqlrefine format path/to/file.sql

# Format in-place (writes files)
tsqlrefine format --write path/to/dir

# Auto-fix issues
tsqlrefine fix --write path/to/file.sql
```

### Running from Source

If running from source code:

```bash
# Build and test
dotnet build src/TsqlRefine.sln -c Release
dotnet test src/TsqlRefine.sln -c Release

# Run the CLI
dotnet run --project src/TsqlRefine.Cli -c Release -- lint path/to/file.sql
dotnet run --project src/TsqlRefine.Cli -c Release -- format path/to/file.sql
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
- Release process: `docs/release.md`

## Releases

Release notes and downloads are available on the [Releases page](https://github.com/imasa/tsqlrefine/releases).

For release process and versioning strategy, see [docs/release.md](docs/release.md).

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

MIT License - see the [LICENSE](LICENSE) file for details.
