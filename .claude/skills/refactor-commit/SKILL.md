---
name: refactor-commit
description: Refactor code and commit changes safely. Use when asked to refactor files, reorganize code, rename/move classes, or clean up code with a commit at the end.
---

# Refactor and Commit

Refactor target files and commit the result after all tests pass.

## Workflow

1. Read the target file(s) and understand the current structure
2. Identify refactoring opportunities based on user request
3. Make changes following existing patterns in the codebase (prefer AST-based over token-based)
4. Update all namespaces and using statements in both main and test projects
5. Build: `dotnet build src/TsqlRefine.sln -c Release`
6. Fix any build errors before proceeding
7. Run full test suite: `dotnet test src/TsqlRefine.sln -c Release`
8. Fix any test failures
9. Commit with a descriptive message summarizing what was refactored and why

## Rules

- Never commit with failing tests
- Always update namespaces when moving/renaming files
- Build before testing to catch compile errors early
- If a refactoring causes widespread breakage, revert and take a smaller approach
