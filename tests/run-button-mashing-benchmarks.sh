#!/bin/bash

# Button Mashing Benchmark Runner
# Tests DoT decision lockout performance across various mashing frequencies

echo "🚀 ModernWrathCombo Button Mashing Benchmarks"
echo "============================================="
echo ""

# Build the project first
echo "Building project..."
./build.sh --no-version-increment > /dev/null 2>&1

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo "✓ Build successful"
echo ""

# Run the benchmarks
echo "Running button mashing benchmarks..."
echo "This will test DoT decision lockout performance from:"
echo "  • Impossible (1ms intervals)"
echo "  • Extreme (10ms intervals)" 
echo "  • Heavy (50ms intervals)"
echo "  • Normal (100ms intervals)"
echo "  • Relaxed (250ms intervals)"
echo "  • GCD-aligned (2.5s intervals)"
echo "  • Random realistic timing"
echo "  • Lockout expiry behavior"
echo ""

cd tests/Performance

# Check if BenchmarkDotNet is available
if ! dotnet list package | grep -q "BenchmarkDotNet"; then
    echo "Installing BenchmarkDotNet..."
    dotnet add package BenchmarkDotNet
fi

# Run the benchmarks
echo "Starting benchmarks (this may take a few minutes)..."
dotnet run -c Release --project ../../src/ModernWrathCombo.csproj ButtonMashingBenchmarks

echo ""
echo "✓ Benchmarks completed!"
echo ""
echo "📊 Results Analysis:"
echo "==================="
echo "• Double DoT Rate: How many duplicate DoT casts occurred"
echo "• Performance Impact: Execution time per scenario" 
echo "• Lockout Effectiveness: Whether 2s window prevents double-casting"
echo ""
echo "💡 Next Steps:"
echo "• If double DoT rate > 1 in any scenario, increase lockout window"
echo "• If performance is acceptable, current solution is sufficient"
echo "• If issues persist, consider implementing cast state detection"
