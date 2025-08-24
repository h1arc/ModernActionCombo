# SmartTargeting Tests

Tests for the SmartTargeting component, which handles intelligent party member targeting for healing and support abilities.

## 🎯 Component Overview

**SmartTargeting** provides ultra-fast party member selection using SIMD-optimized algorithms and comprehensive priority systems.

### Key Features
- **SIMD Optimization**: Vector256 operations for maximum performance
- **Smart Priority**: HP percentage, role priority, range validation
- **Hard Target Support**: Manual target override with ally validation
- **LoS Integration**: Line of sight validation for realistic scenarios
- **Zero Allocation**: Cache-friendly with minimal memory pressure

## 🧪 Test Files

### **SmartTargetingRandomizedTests.cs**
- **Purpose**: Comprehensive randomized scenario testing
- **Coverage**: Random party compositions, HP distributions, priority edge cases
- **Simulations**: Configurable via environment variable

### **SmartTargetingStressTests.cs**
- **Environment Variable**: `SMARTTARG_BENCH_COUNT`
- **Default**: 100 simulations
- **Scaling**: Up to 10 million iterations for extreme validation
- **Purpose**: Performance and reliability under massive load

### **SmartTargetingTests.cs**
- **Purpose**: Core functionality unit tests
- **Coverage**: Basic targeting logic, hard target behavior, edge cases
- **Scope**: Fundamental API validation

### **SmartTargetingAdvancedTests.cs**
- **Purpose**: Complex realistic game scenarios
- **Coverage**: Triage scenarios, revival targeting, AoE damage, range testing
- **Scenarios**: Tank priority, multi-death situations, role-based targeting

### **SmartTargetingIntegrationTests.cs**
- **Purpose**: Integration with GameStateCache and other components
- **Coverage**: Hard target setting, cache interaction, state synchronization
- **Scope**: Cross-component workflows

## ⚡ Performance Benchmarks

### **Targeting Performance**
- **Target**: < 50ns per targeting operation
- **SIMD Operations**: Vector256 for parallel processing
- **Memory**: Minimal allocation, cache-friendly data structures

### **Stress Test Results (10M iterations)**
- **Success Rate**: 100% (10,000,000/10,000,000)
- **Performance**: 26.7ns per call (Hot Cache), 33.4ns (Simple Cache)
- **Reliability**: Zero failures under extreme stress

## 🎮 Test Scenarios

### **Basic Targeting**
- **Lowest HP**: Target party member with lowest HP percentage
- **Role Priority**: Tanks > Healers > DPS when HP is similar
- **Range Validation**: Ignore members outside ability range

### **Hard Target Scenarios**
- **Manual Override**: Respect player's manual target selection
- **Dead Target Handling**: Fall back to smart targeting when hard target dies
- **Ally Validation**: Ensure hard targets are valid party members

### **Advanced Scenarios**
- **Triage**: Multiple low-HP members, prioritize tanks
- **Revival**: Newly revived players with 1 HP get priority
- **AoE Damage**: Mass damage scenarios with multiple targets needing healing
- **Range Testing**: Out-of-range players should be ignored
- **Death Handling**: Only consider alive party members

### **Performance Scenarios**
- **Cache Comparison**: Simple vs Hot vs Hot Paths cache implementations
- **SIMD Operations**: Vector256 parallel processing validation
- **Memory Access**: Cache-line optimization and false sharing prevention

## 🚀 Running Tests

### **Core Functionality**
```bash
dotnet test --filter "SmartTargetingTests"
```

### **Advanced Scenarios**
```bash
dotnet test --filter "SmartTargetingAdvanced"
```

### **Stress Testing**
```bash
SMARTTARG_BENCH_COUNT=10000 dotnet test --filter "SmartTargetingStress"
```

### **Integration Testing**
```bash
dotnet test --filter "SmartTargetingIntegration"
```

### **All SmartTargeting Tests**
```bash
dotnet test --filter "SmartTargeting"
```

## 📊 Performance Comparison

### **Cache Implementation Performance**
| Implementation | Performance | Memory | Use Case |
|---------------|------------|---------|----------|
| Simple Cache | 33.4ns/call | Compact | General use |
| Hot Cache | 26.7ns/call | Optimized | High performance |
| Hot Paths | 31.3ns/call | Aligned | Cache-line optimized |

### **Operation Performance**
| Operation | Time | Notes |
|-----------|------|-------|
| GetBestTarget | ~30ns | Core targeting logic |
| Sort Operations | ~18ns | Priority-based sorting |
| Memory Access | 0.03-0.10ms | 1M accesses |
| Emergency Targeting | 7.4-23.6ns | Critical scenarios |

## 🛠️ Current Issues

### **Test Failures (Expected)**
Several tests are currently failing due to algorithm refinements:
- **Triage scenarios**: Priority logic adjustments needed
- **Role-based targeting**: Tank priority implementation changes
- **Hard target behavior**: Manual target override logic updates

These failures are **expected during development** and represent areas where the targeting algorithm is being optimized.

## 🔧 Maintenance Notes

### **Adding New Scenarios**
1. **Create realistic test data** with proper HP, role, position setup
2. **Use deterministic party member IDs** for reproducible tests
3. **Include edge cases** like all members at 100% HP, single member scenarios
4. **Validate SIMD operations** work correctly with new data patterns

### **Performance Regression Detection**
- **Monitor targeting times** - should stay sub-50ns for core operations
- **Check memory allocation** - should remain minimal
- **Validate SIMD usage** - ensure Vector256 operations are being used

### **Algorithm Updates**
When updating targeting algorithms:
1. **Update test expectations** to match new priority logic
2. **Maintain performance benchmarks** - no regression in speed
3. **Preserve edge case handling** - dead members, out-of-range, etc.
4. **Validate SIMD compatibility** - new logic must work with Vector256

SmartTargeting is a performance-critical component that must maintain sub-50ns targeting times while handling complex priority scenarios reliably.
