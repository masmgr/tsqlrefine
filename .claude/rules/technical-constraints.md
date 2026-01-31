# Technical Constraints

## Target Framework
- .NET 10.0 (see `global.json` for SDK version)
- C# with nullable reference types enabled

## Code Style
- EditorConfig enforced (`.editorconfig`)
- 4-space indentation for C#
- LF line endings
- UTF-8 encoding

## SQL Parsing Constraints
- Supports `GO` batch separator
- DDL statements (CREATE PROC, etc.) supported
- Dynamic SQL strings (`EXEC(...)`) and string literals are NOT analyzed (treated as opaque text)
- Comments and string content preserved during formatting

## Formatting Philosophy
**Minimal formatting only**:
- Keyword casing normalization (uppercase)
- Identifier casing (with escaping for reserved words like `[Order]`)
- Whitespace normalization (respects .editorconfig settings)
- **Preserves**: Comments, string literals, parenthesis-internal line breaks
- **Does NOT**: Reformat layout, reorder clauses, change structure

## Fix System
Auto-fix infrastructure for rules that support fixing:
- Multiple fixes per diagnostic supported (rule selects best one)
- Overlap detection prevents conflicting edits
- Line-by-line text mapping for position-to-offset conversion
- Re-analysis after fixes to verify remaining issues
- Detailed reporting: `AppliedFix` vs `SkippedFix` with reasons

## Important Notes for Development

- Japanese documentation is present (`docs/` files) - important architectural details are there
- When modifying rules, always add corresponding tests
- When adding new rules, create sample SQL files in `samples/sql/` to demonstrate violations
- **Always use helper classes** when implementing rules:
  - Use `DiagnosticVisitorBase` for AST-based rules (not `TSqlFragmentVisitor` directly)
  - Use `ScriptDomHelpers.GetRange()` instead of duplicating range calculation
  - Use `TokenHelpers` utilities for token analysis
  - Use `RuleHelpers.NoFixes()` for non-fixable rules
- Plugin API must remain stable; changes require version bumping
- ScriptDom is an external dependency - we cannot modify its AST structure
- Exit codes are part of the public contract for CI integration
- All CLI commands are fully implemented and functional
- The fix system infrastructure is complete but no rules currently support auto-fixing (all have `Fixable: false`)
- Use `.claude/` directory for agent-specific instructions and configurations
- Helper classes in `TsqlRefine.Rules.Helpers` are public and available to external plugins
