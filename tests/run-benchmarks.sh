#!/bin/bash

# ModernWrathCombo Benchmark Runner
# This script runs performance benchmarks without Dalamud dependencies

echo "🚀 ModernWrathCombo Performance Benchmarks"
echo "Target: <50ns for action resolution"
echo

cd tests

if [ "$1" = "quick" ]; then
    echo "⚡ Running quick benchmarks (5-10 seconds)..."
    dotnet run --configuration Release -- quick
elif [ "$1" = "standalone" ]; then
    echo "🔧 Running standalone action resolver benchmarks..."
    dotnet run --configuration Release -- standalone
elif [ "$1" = "whm" ]; then
    echo "⚔️  Running WHM combo benchmarks..."
    dotnet run --configuration Release -- whm
elif [ "$1" = "help" ] || [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
    echo "Usage: ./run-benchmarks.sh [mode]"
    echo ""
    echo "Modes:"
    echo "  quick      - Fast 5-10 second validation (recommended)"
    echo "  standalone - Full action resolver benchmarks"
    echo "  whm        - WHM combo-specific benchmarks"
    echo "  (no args)  - Run all benchmarks (comprehensive)"
    echo ""
    echo "Examples:"
    echo "  ./run-benchmarks.sh quick     # Quick validation"
    echo "  ./run-benchmarks.sh           # Full benchmark suite"
    exit 0
else
    echo "🎯 Running all benchmarks (comprehensive)..."
    dotnet run --configuration Release
fi

echo
echo "✅ Benchmarks complete!"
echo "📊 Check the performance score above!"
echo "🎯 Target: All operations <50ns for production readiness"
