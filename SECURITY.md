# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 0.4.x   | :white_check_mark: |
| < 0.4   | :x:                |

Only the latest minor release receives security patches.

## Reporting a Vulnerability

If you discover a security vulnerability in tsqlrefine, please report it
**privately** using one of these methods:

1. **GitHub Security Advisories** (preferred):
   <https://github.com/masmgr/tsqlrefine/security/advisories/new>

2. **Email**: Contact the maintainer via the email address on the
   [GitHub profile](https://github.com/masmgr).

**Please do not open a public issue for security vulnerabilities.**

### What to Include

- Description of the vulnerability
- Steps to reproduce
- Affected version(s)
- Potential impact assessment

### Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial assessment**: Within 1 week
- **Fix or mitigation**: Best effort, targeting the next patch release

## Scope

**In scope:**

- Vulnerabilities in the analysis engine or CLI
- Plugin system security bypasses
- Dependency vulnerabilities affecting tsqlrefine users
- Supply chain issues (compromised builds, packages)

**Out of scope:**

- SQL injection in analyzed SQL code (tsqlrefine is a linter, not an executor)
- Denial of service via extremely large input files (mitigated by `--max-file-size`)

## Security Practices

- GitHub Actions pinned to commit SHAs
- Dependabot enabled for automated dependency updates
- `dotnet list package --vulnerable` runs in CI
- SHA256 checksums published with every GitHub Release
- NuGet packages include symbol packages (.snupkg) for source linking
