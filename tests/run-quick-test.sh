#!/bin/bash

# Quick Button Mashing Test Runner
# Simple validation without full benchmark overhead

echo "🚀 Quick Button Mashing Test"
echo "============================"
echo ""

# Build first
echo "Building project..."
cd /Users/mario/Documents/projects/modernwrathcombo/ModernWrathCombo

if ! ./build.sh > /dev/null 2>&1; then
    echo "❌ Build failed"
    exit 1
fi

echo "✓ Build successful"
echo ""

# Run the quick test
echo "Running quick button mashing validation..."
cd tests

# Run the test directly
dotnet run --project . QuickBenchmarkRunner

echo ""
echo "✓ Test completed!"
