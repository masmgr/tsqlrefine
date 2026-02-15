# Editor Integration Samples

Example configuration files for integrating tsqlrefine into your editor and Git workflow.

## Files

| File | Description |
|------|-------------|
| [vscode-tasks.json](vscode-tasks.json) | VS Code tasks for lint, fix, and format with problem matcher |
| [pre-commit-hook.sh](pre-commit-hook.sh) | Git pre-commit hook that lints staged SQL files |

## Usage

### VS Code Tasks

Copy `vscode-tasks.json` to `.vscode/tasks.json` in your project. Then use `Ctrl+Shift+P` → "Run Task" to lint, fix, or format SQL files.

### Pre-commit Hook

```bash
cp samples/editor/pre-commit-hook.sh .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

## More Examples

See [Editor Integration Guide](../../docs/editor-integration.md) for:

- VS Code keyboard shortcuts
- pre-commit framework configuration
- JetBrains Rider / DataGrip setup
