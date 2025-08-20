# ModernWrathCombo Performance Benchmarks

This document explains the performance benchmark system and scoring methodology.

## Quick Start

Run a fast 5-10 second performance validation:
```bash
./run-benchmarks.sh quick
```

## Benchmark Modes

| Mode | Duration | Purpose |
|------|----------|---------|
| `quick` | 5-10 seconds | Fast validation for development |
| `standalone` | 2-3 minutes | Full action resolver performance analysis |
| `whm` | 1-2 minutes | WHM combo-specific benchmarks |
| (no args) | 3-5 minutes | Comprehensive benchmark suite |

## Performance Scoring System (0-100)

Our scoring system evaluates performance against FFXIV action resolution requirements:

### 🟢 Excellent (90-100 points)
- **Fast Path**: <5ns - Ultra-fast unhandled action resolution
- **Game State**: <20ns - Exceptional performance with full context
- **Batch Operations**: <5ns per action - Perfect efficiency

### 🟡 Good (70-89 points)  
- **Fast Path**: 5-20ns - Meeting targets with room for optimization
- **Game State**: 20-50ns - Good performance within target range
- **Batch Operations**: 5-20ns per action - Solid efficiency

### 🔴 Needs Improvement (<70 points)
- **Fast Path**: >20ns - Below expected performance
- **Game State**: >50ns - Exceeding target thresholds
- **Batch Operations**: >20ns per action - Inefficient processing

## Target Performance Metrics

### Primary Target: <50ns Action Resolution
All core game state action resolution should complete in under 50 nanoseconds to ensure:
- No perceptible latency during gameplay
- Ability to handle burst action sequences
- Minimal CPU overhead in combat scenarios

### Secondary Targets:
- **Fast Path**: <5ns for unhandled actions
- **Batch Efficiency**: <10ns per action in bulk operations
- **Zero Allocations**: No GC pressure in hot paths
- **Memory Efficiency**: Minimal heap allocations

## Understanding Results

### Performance Categories

1. **QuickResolve_FastPath**: Tests the fastest code path for actions without handlers
2. **QuickResolve_WithGameState**: Tests full game state resolution (our primary target)
3. **QuickBatch_10Actions**: Tests batch processing efficiency

### Score Interpretation

- **95-100**: Exceptional performance exceeding all targets
- **85-94**: Excellent performance well above requirements  
- **70-84**: Good performance meeting production requirements
- **50-69**: Acceptable performance with potential optimization needs
- **<50**: Below acceptable thresholds, optimization required

## Hardware Context

Benchmarks are designed to be hardware-agnostic but results may vary based on:
- CPU architecture (x64 vs ARM64)
- Memory speed and cache sizes
- .NET runtime optimizations
- System load during testing

The scoring system accounts for typical gaming hardware performance characteristics.

## Integration with Development

### Continuous Validation
Run `./run-benchmarks.sh quick` after any performance-critical changes to validate:
- Action resolver modifications
- Game state processing updates
- Combo logic optimizations

### Performance Regression Detection
A score below 70 may indicate:
- Performance regression in recent changes
- Suboptimal algorithm implementation
- Need for code review and optimization

### Production Readiness
An overall score of 80+ indicates the system is ready for production use with excellent FFXIV gameplay performance.
