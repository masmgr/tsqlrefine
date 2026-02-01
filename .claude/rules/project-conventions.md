# Project Conventions

Universal development conventions that apply to all code in this repository.

## Target Framework

- .NET 10.0 (see `global.json` for SDK version)
- C# with nullable reference types enabled
- All projects target `net10.0`

## Code Style

EditorConfig enforced (`.editorconfig`):
- 4-space indentation for C#
- LF line endings
- UTF-8 encoding
- File-scoped namespaces
- Nullable reference types enabled

## Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Rule class | PascalCase + "Rule" | `AvoidSelectStarRule` |
| Test class | PascalCase + "Tests" | `AvoidSelectStarRuleTests` |
| Rule ID | kebab-case | `avoid-select-star` |
| Visitor class | Private nested + "Visitor" | `AvoidSelectStarVisitor` |
| Helper class | PascalCase + purpose | `TokenHelpers`, `ScriptDomHelpers` |

## Development Requirements

- **Always add tests** when modifying or adding code
- **Never modify existing tests** without understanding why they exist
- **Run tests** before committing: `dotnet test src/TsqlRefine.sln -c Release`
- **Sample SQL files** required for new rules in `samples/sql/`

## Documentation

- Key docs in `docs/` directory
- Rule documentation auto-generated in `docs/Rules/`
- Configuration schemas in `schemas/`

## Public Contracts

These are stable APIs that require careful consideration before changing:

1. **Exit codes** (`ExitCodes.cs`) - Used by CI systems
2. **Plugin API** (`PluginApi.CurrentVersion`) - Version bump required for changes
3. **CLI command structure** - Breaking changes affect users
4. **JSON output format** - Used by integrations

## Build Commands

```powershell
# Build entire solution
dotnet build src/TsqlRefine.sln -c Release

# Run all tests
dotnet test src/TsqlRefine.sln -c Release

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestName"
```
