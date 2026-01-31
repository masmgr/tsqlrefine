# List Rules

List available tsqlrefine rules with filtering and details.

## Usage

```
/list-rules [options]

Options:
  --category <name>   Filter by category (Safety, Correctness, Performance, Style, Security, Schema, Transactions, Debug)
  --severity <level>  Filter by severity (Error, Warning, Information, Hint)
  --fixable           Show only fixable rules
  --json              Output as JSON
  --verbose           Show detailed rule information
```

Examples:
- `/list-rules` - List all rules
- `/list-rules --category Safety` - List safety rules only
- `/list-rules --severity Error` - List error-level rules
- `/list-rules --verbose` - Show detailed information

## Instructions

You are a rule discovery assistant for tsqlrefine.

### Workflow

1. **Run List Command**
   - Execute the list-rules CLI command
   - Apply any specified filters

2. **Present Results**
   - Group rules by category
   - Show rule ID, description, severity
   - Include fixable status if relevant

### Commands

#### List all rules
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules
```

#### List rules as JSON
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- list-rules --output json
```

### Rule Categories

| Category | Description | Count (approx) |
|----------|-------------|----------------|
| **Safety** | Prevent data loss/corruption | 7 |
| **Correctness** | Fix logical errors | 13 |
| **Performance** | Optimize query performance | 5 |
| **Style** | Code consistency | 18+ |
| **Security** | Security vulnerabilities | 3 |
| **Schema** | Schema design issues | 4 |
| **Transactions** | Transaction handling | 9 |
| **Debug** | Debug/development issues | 12+ |

### Output Format

```
## Available Rules (85 total)

### Safety (7 rules)
| Rule ID | Description | Severity |
|---------|-------------|----------|
| dml-without-where | UPDATE/DELETE without WHERE clause | Error |
| avoid-merge | Avoid MERGE statement | Warning |
| ... | ... | ... |

### Correctness (13 rules)
| Rule ID | Description | Severity |
|---------|-------------|----------|
| avoid-null-comparison | Use IS NULL instead of = NULL | Warning |
| ... | ... | ... |

[continue for all categories]
```

### Preset Rulesets

Quick reference for built-in presets:
- **recommended** (49 rules): Balanced for production
- **strict** (86 rules): Maximum enforcement
- **pragmatic** (30 rules): Minimal noise
- **security-only** (10 rules): Security focus only
