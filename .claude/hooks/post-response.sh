#!/bin/bash
# Claude Code post-response hook
# Runs format, build, and test after Claude makes changes

set -e

echo "🔄 Running post-development checks..."

# Step 1: Build
echo "📦 Building solution..."
if dotnet build src/TsqlRefine.sln -c Release --nologo --verbosity minimal; then
    echo "✅ Build succeeded"
else
    echo "❌ Build failed"
    exit 1
fi

# Step 2: Test
echo "🧪 Running tests..."
if dotnet test src/TsqlRefine.sln -c Release --nologo --verbosity minimal --no-build; then
    echo "✅ Tests passed"
else
    echo "❌ Tests failed"
    exit 1
fi

echo "✅ All checks passed!"
exit 0
