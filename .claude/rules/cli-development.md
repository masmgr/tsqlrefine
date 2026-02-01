---
paths:
  - "src/TsqlRefine.Cli/**/*.cs"
  - "tests/TsqlRefine.Cli.Tests/**/*.cs"
---

# CLI Layer Development

Development patterns for the TsqlRefine.Cli project - command-line interface.

## Architecture

Built on **System.CommandLine 2.0.0** with subcommand-based structure.

Key components:
- `CliApp`: Main dispatcher for all commands
- `CliParser`: Subcommand-based argument parsing with typed options
- `CliArgs`: Parsed argument record
- `CommandExecutor`: Command execution logic
- `ExitCodes`: Exit code definitions

## Command Structure

```
tsqlrefine <command> [options] [paths...]

Commands:
  lint               Analyze SQL files for rule violations (default)
  format             Format SQL files (keyword casing, whitespace)
  fix                Auto-fix issues that support fixing
  init               Initialize configuration files
  print-config       Print effective configuration
  print-format-config  Print effective formatting options
  list-rules         List available rules
  list-plugins       List loaded plugins
```

## Adding a New Command

### Step 1: Define Command in CliParser

Add command definition in `src/TsqlRefine.Cli/CliParser.cs`:

```csharp
var myCommand = new Command("my-command", "Description of my command")
{
    // Add options
    configOption,
    outputOption,
};
myCommand.SetHandler(/* handler */);
rootCommand.AddCommand(myCommand);
```

### Step 2: Add Handler in CliApp

Add handler method in `src/TsqlRefine.Cli/CliApp.cs`:

```csharp
private async Task<int> RunMyCommandAsync(CliArgs args, CancellationToken ct)
{
    // Implementation
    return ExitCodes.Success;
}
```

### Step 3: Update Documentation

Update help text and documentation in `docs/cli.md`.

## Options by Command

| Option | lint | format | fix | init | print-config | print-format-config | list-rules | list-plugins |
|--------|:----:|:------:|:---:|:----:|:------------:|:-------------------:|:----------:|:------------:|
| `-c, --config` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `-g, --ignorelist` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--detect-encoding` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--stdin` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--output` | ✓ | ✓ | ✓ | - | ✓ | ✓ | ✓ | ✓ |
| `--severity` | ✓ | - | ✓ | - | - | - | - | - |
| `--preset` | ✓ | - | ✓ | - | - | - | - | - |
| `--compat-level` | ✓ | ✓ | ✓ | - | - | - | - | - |
| `--ruleset` | ✓ | - | ✓ | - | - | - | - | - |
| `--rule` | - | - | ✓ | - | - | - | - | - |
| `--indent-style` | - | ✓ | - | - | - | ✓ | - | - |
| `--indent-size` | - | ✓ | - | - | - | ✓ | - | - |
| `--show-sources` | - | - | - | - | - | ✓ | - | - |
| `--verbose` | - | - | - | - | - | - | ✓ | - |
| `paths...` | ✓ | ✓ | ✓ | - | - | - | - | - |

## Output Formatting

Output formatting is in `CommandExecutor.cs`:

### Text Output (Default)

ESLint/GCC-compatible format:
```
file.sql:1:8: Warning: Avoid SELECT * - use explicit column list (avoid-select-star)
```

### JSON Output

Uses System.Text.Json with settings from `JsonDefaults.cs`:

```json
{
  "files": [
    {
      "path": "file.sql",
      "diagnostics": [
        {
          "range": { "start": { "line": 0, "character": 7 }, "end": { "line": 0, "character": 8 } },
          "message": "Avoid SELECT * - use explicit column list",
          "severity": "Warning",
          "code": "avoid-select-star"
        }
      ]
    }
  ]
}
```

## Exit Codes

Defined in `ExitCodes.cs` - **public contract for CI integration**:

| Code | Meaning |
|------|---------|
| 0 | Success (no violations or filtered to zero) |
| 1 | Rule violations found |
| 2 | Parse error (syntax error, GO batch split failure) |
| 3 | Config error (invalid config, bad compat level) |
| 4 | Runtime exception (internal error) |

## Testing CLI

Integration tests in `tests/TsqlRefine.Cli.Tests/`:

```csharp
[Fact]
public async Task Lint_WithViolation_ReturnsExitCode1()
{
    var result = await CliApp.RunAsync(new[] { "lint", "file.sql" });
    Assert.Equal(1, result);
}
```

## Common Tasks

### Run CLI During Development

```powershell
# Lint
dotnet run --project src/TsqlRefine.Cli -c Release -- lint file.sql

# Format
dotnet run --project src/TsqlRefine.Cli -c Release -- format file.sql

# Fix
dotnet run --project src/TsqlRefine.Cli -c Release -- fix file.sql

# Lint from stdin
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin
```

### Initialize Configuration

```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- init
```

Creates:
- `tsqlrefine.json`: Default configuration file
- `tsqlrefine.ignore`: Default ignore patterns file

## Reference Files

- Main entry: `src/TsqlRefine.Cli/Program.cs`
- App dispatcher: `src/TsqlRefine.Cli/CliApp.cs`
- Parser: `src/TsqlRefine.Cli/CliParser.cs`
- Exit codes: `src/TsqlRefine.Cli/ExitCodes.cs`
- Documentation: `docs/cli.md`
