# Quick SQL Lint

Quickly lint SQL code using tsqlrefine CLI.

## Usage

```
/lint [options] <sql-or-file>

Options:
  --preset <name>     Use preset (recommended, strict, pragmatic, security-only)
  --severity <level>  Filter by severity (error, warning, information, hint)
  --json              Output as JSON
  --rule <id>         Check specific rule only
```

Examples:
- `/lint SELECT * FROM users` - Lint inline SQL
- `/lint path/to/file.sql` - Lint a file
- `/lint --preset strict SELECT * FROM users` - Use strict ruleset
- `/lint --rule avoid-select-star SELECT * FROM users` - Check specific rule

## Instructions

You are a SQL linting assistant for tsqlrefine.

### Workflow

1. **Parse Input**
   - If input looks like a file path (ends with `.sql` or contains `/`), lint the file
   - Otherwise, treat as inline SQL

2. **Run Lint Command**
   - For inline SQL: Use `--stdin` with echo pipe
   - For file: Pass file path directly
   - Apply any specified options (preset, severity, rule)

3. **Report Results**
   - Show violations with file:line:col format
   - Include rule ID and message
   - Summarize total issues found

### Commands

#### Lint inline SQL
```powershell
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin
```

#### Lint inline SQL with JSON output
```powershell
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --output json
```

#### Lint file
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- lint path/to/file.sql
```

#### Lint with preset
```powershell
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --preset strict
```

#### Lint with severity filter
```powershell
echo "SELECT * FROM users;" | dotnet run --project src/TsqlRefine.Cli -c Release -- lint --stdin --severity error
```

### Output Format

Report results in this format:
```
## Lint Results

**Input**: [SQL snippet or file path]
**Preset**: [preset name if specified]

### Violations (N found)

| Line:Col | Severity | Rule | Message |
|----------|----------|------|---------|
| 1:8 | Warning | avoid-select-star | Avoid SELECT * |

### Summary
- Errors: N
- Warnings: N
- Total: N
```

If no violations: "✅ No violations found"
