---
name: format
description: Format SQL code using tsqlrefine CLI. Use when: formatting T-SQL, normalizing keyword casing, fixing indentation, applying consistent SQL style. Respects .editorconfig settings.
---

# Quick SQL Format

Format SQL with `tsqlrefine format`.

## Commands

```powershell
# Inline SQL
echo "select * from users where id=1" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin

# File (writes in place)
dotnet run --project src/TsqlRefine.Cli -c Release -- format path/to/file.sql

# Custom indentation
echo "select * from users" | dotnet run --project src/TsqlRefine.Cli -c Release -- format --stdin --indent-style spaces --indent-size 4
```

## Formatting Pipeline

1. **ScriptDomElementCaser** - Uppercase keywords, functions, data types
2. **WhitespaceNormalizer** - Normalize indentation (respects .editorconfig)
3. **InlineSpaceNormalizer** - Space after commas, remove duplicate spaces

## Options

| Option | Values |
|--------|--------|
| `--indent-style` | `spaces`, `tabs` |
| `--indent-size` | Number (default: 4) |

## Output

- stdin input: Formatted SQL to stdout
- file input: Written directly to file

## Notes

- Respects `.editorconfig` for `indent_style` and `indent_size`
- CLI options override `.editorconfig`
- Protected regions: comments and string literals are not modified
