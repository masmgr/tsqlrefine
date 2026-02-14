# Editor Integration

This guide shows how to integrate tsqlrefine into your editor and development workflow.

## Visual Studio Code

### Tasks

Add these tasks to `.vscode/tasks.json` to run tsqlrefine from VS Code:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "TsqlRefine: Lint Current File",
      "type": "shell",
      "command": "tsqlrefine",
      "args": ["lint", "${file}"],
      "problemMatcher": {
        "owner": "tsqlrefine",
        "fileLocation": ["relative", "${workspaceFolder}"],
        "pattern": {
          "regexp": "^(.+):(\\d+):(\\d+):\\s+(error|warning|info)\\s+([\\w-]+):\\s+(.+)$",
          "file": 1,
          "line": 2,
          "column": 3,
          "severity": 4,
          "code": 5,
          "message": 6
        }
      },
      "presentation": {
        "reveal": "always",
        "panel": "shared"
      }
    },
    {
      "label": "TsqlRefine: Lint Workspace",
      "type": "shell",
      "command": "tsqlrefine",
      "args": ["lint", "src/**/*.sql"],
      "problemMatcher": {
        "owner": "tsqlrefine",
        "fileLocation": ["relative", "${workspaceFolder}"],
        "pattern": {
          "regexp": "^(.+):(\\d+):(\\d+):\\s+(error|warning|info)\\s+([\\w-]+):\\s+(.+)$",
          "file": 1,
          "line": 2,
          "column": 3,
          "severity": 4,
          "code": 5,
          "message": 6
        }
      }
    },
    {
      "label": "TsqlRefine: Fix Current File",
      "type": "shell",
      "command": "tsqlrefine",
      "args": ["fix", "--write", "${file}"],
      "problemMatcher": []
    },
    {
      "label": "TsqlRefine: Format Current File",
      "type": "shell",
      "command": "tsqlrefine",
      "args": ["format", "--write", "${file}"],
      "problemMatcher": []
    }
  ]
}
```

**Usage**: Press `Ctrl+Shift+P` → "Run Task" → select a TsqlRefine task.

The `problemMatcher` parses tsqlrefine's text output format and displays violations in VS Code's Problems panel with clickable file locations.

### Keyboard Shortcuts

Add shortcuts to `.vscode/keybindings.json`:

```json
[
  {
    "key": "ctrl+shift+l",
    "command": "workbench.action.tasks.runTask",
    "args": "TsqlRefine: Lint Current File",
    "when": "editorLangId == sql"
  },
  {
    "key": "ctrl+shift+alt+f",
    "command": "workbench.action.tasks.runTask",
    "args": "TsqlRefine: Format Current File",
    "when": "editorLangId == sql"
  }
]
```

## Git Pre-commit Hook

Automatically lint staged SQL files before each commit.

### Simple Hook (No Dependencies)

Create `.git/hooks/pre-commit`:

```bash
#!/bin/sh

STAGED_SQL=$(git diff --cached --name-only --diff-filter=ACMR | grep '\.sql$')

if [ -z "$STAGED_SQL" ]; then
  exit 0
fi

echo "Linting staged SQL files..."
tsqlrefine lint --preset security-only $STAGED_SQL

RESULT=$?

if [ $RESULT -eq 1 ]; then
  echo ""
  echo "SQL lint violations found. Fix the issues or use 'git commit --no-verify' to skip."
  exit 1
elif [ $RESULT -gt 1 ]; then
  echo "TsqlRefine error (exit code $RESULT)."
  exit 1
fi
```

Make it executable:

```bash
chmod +x .git/hooks/pre-commit
```

### pre-commit Framework

Add to `.pre-commit-config.yaml`:

```yaml
repos:
  - repo: local
    hooks:
      - id: tsqlrefine-lint
        name: TsqlRefine SQL Linter
        entry: tsqlrefine lint --preset security-only
        language: system
        files: \.sql$
        pass_filenames: true
```

Install:

```bash
pip install pre-commit
pre-commit install
```

## JetBrains Rider / DataGrip

Configure as an External Tool:

1. **Settings** → **Tools** → **External Tools** → **+**
2. Set:
   - **Name**: TsqlRefine Lint
   - **Program**: `tsqlrefine`
   - **Arguments**: `lint $FilePath$`
   - **Working directory**: `$ProjectFileDir$`
3. **Usage**: Right-click a `.sql` file → **External Tools** → **TsqlRefine Lint**

Add a second tool for formatting:
- **Name**: TsqlRefine Format
- **Arguments**: `format --write $FilePath$`

## See Also

- [CLI Specification](cli.md) — Full command reference and `--quiet` mode for machine consumption
- [CI Integration Guide](ci-integration.md) — Pipeline setup for GitHub Actions, Azure Pipelines, GitLab CI
- [Configuration Guide](configuration.md) — `tsqlrefine.json` and preset rulesets
