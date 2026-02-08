# Release Procedure

This document describes the procedure for releasing a new version of tsqlrefine.

## Versioning Policy

tsqlrefine follows [Semantic Versioning 2.0.0](https://semver.org/).

### Version Number Format

- **MAJOR.MINOR.PATCH** (e.g., `1.0.0`)
- **MAJOR.MINOR.PATCH-prerelease** (e.g., `0.1.0-alpha`, `1.0.0-beta.1`)

### Version Update Rules

- **MAJOR**: Breaking changes
  - Breaking changes to PluginSDK API
  - Breaking changes to CLI arguments or output format
  - Breaking changes to configuration file format

- **MINOR**: Backward-compatible feature additions
  - Adding new rules
  - Adding new CLI commands or options
  - Adding new API features (PluginSDK extensions)

- **PATCH**: Backward-compatible bug fixes
  - Fixing rule false positives
  - Fixing formatter bugs
  - Performance improvements

### Pre-release Versions

- **alpha**: Unstable development version (may have breaking changes)
- **beta**: Feature-frozen, under testing (bug fixes only)
- **rc**: Release candidate (critical bug fixes only)

## Release Procedure

### 1. Determine Version Number

Determine the version number for the next release.

- Check current version in `Directory.Build.props` under `VersionPrefix`
- Determine appropriate version number based on changes

### 2. Update Version Number

Edit `Directory.Build.props` to update the version number.

```xml
<VersionPrefix>0.2.0</VersionPrefix>
<VersionSuffix Condition="'$(VersionSuffix)' == ''">alpha</VersionSuffix>
```

For pre-release versions:
- Set `VersionSuffix` to `alpha`, `beta`, `rc.1`, etc.

For stable releases:
- Remove `VersionSuffix` or set to empty string
- Or specify `/p:VersionSuffix=` at build time

### 3. Update CHANGELOG

Create/update `CHANGELOG.md` to record release contents.

```markdown
## [0.2.0] - 2026-01-29

### Added
- New rule: `require-schema-prefix`
- `--parallel` option for parallel processing support

### Changed
- Performance improvement: 2x faster analysis for large SQL files

### Fixed
- Fixed false positive for `avoid-select-star` rule inside CTEs
```

### 4. Create Commit and Tag

```bash
# Commit changes
git add Directory.Build.props CHANGELOG.md
git commit -m "chore: bump version to 0.2.0"

# Create tag (v prefix required)
git tag v0.2.0

# Push to remote
git push origin main
git push origin v0.2.0
```

### 5. Automated Release via GitHub Actions

Pushing a tag automatically triggers GitHub Actions to:

1. Build and test
2. Create NuGet packages (TsqlRefine CLI tool + TsqlRefine.PluginSdk library)
3. Create GitHub Release (with package files attached)
4. Publish to NuGet.org (stable versions only, pre-releases excluded)

Check release progress at:
- GitHub Actions: https://github.com/imasa/tsqlrefine/actions

### 6. Edit Release Notes

After GitHub Release is created, edit release notes if needed:

1. Go to https://github.com/imasa/tsqlrefine/releases
2. Select the latest release
3. Click "Edit release"
4. Enhance release notes (Breaking changes, Migration guide, etc.)

## Local Package Creation

For testing the release process or creating packages locally:

```bash
# Build (pre-release version)
dotnet build src/TsqlRefine.sln -c Release

# Build (stable version)
dotnet build src/TsqlRefine.sln -c Release /p:VersionSuffix=

# Create packages
dotnet pack src/TsqlRefine.Cli/TsqlRefine.Cli.csproj -c Release /p:VersionSuffix=
dotnet pack src/TsqlRefine.PluginSdk/TsqlRefine.PluginSdk.csproj -c Release /p:VersionSuffix=

# Output location
# nupkg/TsqlRefine.0.2.0.nupkg
# nupkg/TsqlRefine.0.2.0.snupkg (symbols package)
# nupkg/TsqlRefine.PluginSdk.0.2.0.nupkg
# nupkg/TsqlRefine.PluginSdk.0.2.0.snupkg (symbols package)
```

## Installation Methods

### Install as Global Tool

```bash
# Install latest version from NuGet.org
dotnet tool install --global TsqlRefine

# Install specific version
dotnet tool install --global TsqlRefine --version 0.2.0

# Install pre-release version
dotnet tool install --global TsqlRefine --version 0.2.0-alpha --prerelease

# Update
dotnet tool update --global TsqlRefine

# Uninstall
dotnet tool uninstall --global TsqlRefine
```

### Install as Local Tool

For managing tools per project:

```bash
# Create tool manifest
dotnet new tool-manifest

# Install as local tool
dotnet tool install TsqlRefine

# Run
dotnet tsqlrefine --help

# Update
dotnet tool update TsqlRefine
```

### Install from Local Package

For development or testing with locally built packages:

```bash
# Create package
dotnet pack src/TsqlRefine.Cli/TsqlRefine.Cli.csproj -c Release /p:VersionSuffix=

# Install from local source
dotnet tool install --global TsqlRefine --add-source ./nupkg --version 0.2.0

# Or specify package directly
dotnet tool install --global --add-source ./nupkg TsqlRefine
```

## Troubleshooting

### Package Not Found

After publishing to NuGet.org, it may take a few minutes for the package to be indexed.

### Old Version Gets Installed

Clear cache and reinstall:

```bash
dotnet nuget locals all --clear
dotnet tool update --global TsqlRefine
```

### GitHub Actions Workflow Fails

- Verify `NUGET_API_KEY` secret is configured
  - Settings > Secrets and variables > Actions > Repository secrets
- Verify all tests pass
- Check for build errors

## References

- [Semantic Versioning 2.0.0](https://semver.org/)
- [.NET Global Tools documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- [Creating and publishing NuGet packages](https://learn.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package-using-the-dotnet-cli)
