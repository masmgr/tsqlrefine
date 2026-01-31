# Quick SQL Format

Quickly format SQL code using tsqlrefine CLI.

## Usage

```
/format [options] <sql-or-file>

Options:
  --write             Write changes to file (file input only)
  --indent-style <s>  Indentation style (spaces, tabs)
  --indent-size <n>   Indentation size (default: 4)
```

Examples:
- `/format select * from users` - Format inline SQL
- `/format path/to/file.sql` - Format a file
- `/format --write path/to/file.sql` - Format in place

## Instructions

You are a SQL formatting assistant for tsqlrefine.

### Workflow

1. **Parse Input**
   - If input looks like a file path (ends with `.sql` or contains `/`), format the file
   - Otherwise, treat as inline SQL

2. **Run Format Command**
   - For inline SQL: Use `--stdin` with echo pipe
   - For file: Pass file path directly
   - Apply any specified options (write, indent)

3. **Show Results**
   - Display formatted SQL
   - If --write, confirm file was updated

### Commands

#### Format inline SQL
```powershell
echo "select * from users where id=1" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin
```

#### Format file
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- format path/to/file.sql
```

#### Format file in place
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- format --write path/to/file.sql
```

#### Format with custom indentation
```powershell
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin --indent-style tabs --indent-size 2
```

### Formatting Pipeline

tsqlrefine applies these transformations:
1. **ScriptDomElementCaser** - Uppercase keywords, functions, data types
2. **WhitespaceNormalizer** - Normalize indentation (respects .editorconfig)
3. **InlineSpaceNormalizer** - Space after commas, remove duplicate spaces
4. **CommaStyleTransformer** - Optional: trailing to leading commas

### Output Format

Show results as:
```
## Formatted SQL

**Input**: [SQL snippet or file path]

### Result
```sql
SELECT *
FROM users
WHERE id = 1
```

### Changes Applied
- Keywords uppercased
- Whitespace normalized
- [other changes]
```

