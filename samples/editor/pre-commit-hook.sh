#!/bin/sh
# Git pre-commit hook for tsqlrefine
#
# Install:
#   cp samples/editor/pre-commit-hook.sh .git/hooks/pre-commit
#   chmod +x .git/hooks/pre-commit
#
# See docs/editor-integration.md for pre-commit framework integration.

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
