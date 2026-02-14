# CI Integration Guide

This guide shows how to integrate tsqlrefine into your CI/CD pipeline.

## GitHub Actions

### Basic Lint Check

Fail the build when violations are found:

```yaml
name: SQL Lint

on:
  pull_request:
    paths:
      - '**.sql'
  push:
    branches: [main, develop]

jobs:
  lint-sql:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Install TsqlRefine
        run: dotnet tool install --global TsqlRefine

      - name: Lint SQL files
        run: tsqlrefine lint src/**/*.sql
```

Exit code `1` (violations found) automatically fails the workflow step.

### JSON Output with Artifact Upload

Save lint results as a downloadable artifact:

```yaml
name: SQL Lint (JSON)

on:
  pull_request:
    paths:
      - '**.sql'

jobs:
  lint-sql:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Install TsqlRefine
        run: dotnet tool install --global TsqlRefine

      - name: Lint SQL files
        run: tsqlrefine lint --output json src/**/*.sql > lint-results.json
        continue-on-error: true

      - name: Upload lint results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: tsqlrefine-results
          path: lint-results.json

      - name: Fail if violations found
        run: tsqlrefine lint --quiet src/**/*.sql
```

### Diff-Only Linting (Large Repositories)

Only lint SQL files changed in the pull request:

```yaml
name: SQL Lint (Changed Files)

on:
  pull_request:
    paths:
      - '**.sql'

jobs:
  lint-changed-sql:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Install TsqlRefine
        run: dotnet tool install --global TsqlRefine

      - name: Get changed SQL files
        id: changed
        run: |
          FILES=$(git diff --name-only --diff-filter=ACMR origin/${{ github.base_ref }}...HEAD -- '*.sql' | tr '\n' ' ')
          echo "files=$FILES" >> "$GITHUB_OUTPUT"

      - name: Lint changed files
        if: steps.changed.outputs.files != ''
        run: tsqlrefine lint ${{ steps.changed.outputs.files }}
```

### Multi-Preset Strategy

Block on security issues; report best-practice warnings without failing:

```yaml
name: SQL Lint (Multi-Level)

on:
  pull_request:

jobs:
  security-check:
    name: Security (Blocking)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - run: dotnet tool install --global TsqlRefine
      - name: Security lint
        run: tsqlrefine lint --preset security-only src/**/*.sql

  best-practices:
    name: Best Practices (Non-blocking)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - run: dotnet tool install --global TsqlRefine
      - name: Recommended lint
        run: tsqlrefine lint --preset recommended src/**/*.sql || true
```

## Azure Pipelines

```yaml
trigger:
  branches:
    include: [main, develop]

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '10.x'

  - script: dotnet tool install --global TsqlRefine
    displayName: 'Install TsqlRefine'

  - script: tsqlrefine lint src/**/*.sql
    displayName: 'Lint SQL files'
```

## GitLab CI

```yaml
sql-lint:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:10.0
  before_script:
    - dotnet tool install --global TsqlRefine
    - export PATH="$PATH:$HOME/.dotnet/tools"
  script:
    - tsqlrefine lint src/**/*.sql
  rules:
    - changes:
        - '**/*.sql'
```

## Exit Codes

| Code | Meaning | CI Action |
|------|---------|-----------|
| 0 | No violations | Pass |
| 1 | Rule violations found | Fail (default) or continue with `|| true` |
| 2 | Parse error | Fail |
| 3 | Config error | Fail |
| 4 | Runtime exception | Fail |

## Best Practices

### Pin Tool Version

Use a local tool manifest for reproducible builds:

```bash
# In your repository root (one-time setup)
dotnet new tool-manifest
dotnet tool install TsqlRefine --version 0.4.0
```

Then in CI:

```yaml
- name: Restore tools
  run: dotnet tool restore

- name: Lint SQL
  run: dotnet tsqlrefine lint src/**/*.sql
```

The manifest file (`.config/dotnet-tools.json`) should be committed to the repository.

### Exclude Generated Files

Use `tsqlrefine.ignore` to skip generated or vendor SQL:

```gitignore
vendor/
**/generated/**
**/migrations/**
```

### Cache .NET Tools

Speed up CI with tool caching:

```yaml
- uses: actions/cache@v4
  with:
    path: ~/.dotnet/tools
    key: dotnet-tools-${{ runner.os }}-${{ hashFiles('.config/dotnet-tools.json') }}

- run: dotnet tool restore
```

## Future: SARIF Output

SARIF (Static Analysis Results Interchange Format) support is planned for a future release. This will enable native GitHub Code Scanning integration and inline PR annotations. Currently, use `--output json` for machine-readable results.

## See Also

- [CLI Specification](cli.md)
- [Configuration Guide](configuration.md)
- [Editor Integration](editor-integration.md)
