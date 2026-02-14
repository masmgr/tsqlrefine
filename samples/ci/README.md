# CI Workflow Samples

Example CI/CD workflow files for integrating tsqlrefine into your pipeline.

## Files

| File | Description |
|------|-------------|
| [github-actions-basic.yml](github-actions-basic.yml) | Minimal GitHub Actions workflow — lint all SQL files on push/PR |
| [github-actions-advanced.yml](github-actions-advanced.yml) | Advanced workflow — diff-only linting with JSON artifact upload |

## Usage

Copy one of these files to `.github/workflows/sql-lint.yml` in your repository and adjust the file paths.

## More Examples

See [CI Integration Guide](../../docs/ci-integration.md) for:

- Azure Pipelines and GitLab CI examples
- Multi-preset strategy (security blocking + best-practice warnings)
- Tool version pinning with `dotnet tool restore`
- Caching and performance tips
