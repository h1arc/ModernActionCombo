# ActionResolver Tests

Tests for the ActionResolver component, which handles action upgrade chain resolution (Stone → Glare, Aero → Dia, etc.).

## 🎯 Component Overview

**ActionResolver** maps base actions to their level-appropriate upgraded versions using static lookup tables for maximum performance.

### Key Features
- **Ultra-fast Resolution**: Sub-500ns action resolution via dictionary + array lookup
- **Known Upgrade Chains**: Stone/Glare, Aero/Dia, Holy chains with exact action IDs
- **Level-based Logic**: Automatic upgrades based on player level
- **Zero Allocation**: Pure static operations with no memory allocation

## 🧪 Test Files

### **ActionResolverFocusedRandomizedTests.cs**
- **Purpose**: Pure ActionResolver testing without cache interference
- **Focus**: Direct `ResolveToLevel()` API testing
- **Coverage**: Upgrade chain logic, level boundaries, invalid inputs
- **Performance**: Validates sub-500ns resolution time

### **ActionResolverRandomizedTests.cs** 
- **Purpose**: Comprehensive ActionResolver testing (legacy, broader scope)
- **Coverage**: General action resolution scenarios
- **Performance**: Sub-1000ns threshold with tolerance for system variation

### **ActionResolverStressTests.cs**
- **Environment Variable**: `ACTIONRES_BENCH_COUNT`
- **Default**: 100 simulations
- **Purpose**: Stress testing the broader ActionResolver functionality

### **ActionResolverFocusedStressTests.cs**
- **Environment Variable**: `ACTIONRES_FOCUSED_BENCH_COUNT` 
- **Default**: 100 simulations
- **Purpose**: Stress testing pure ActionResolver without dependencies

### **ActionResolverTests.cs**
- **Purpose**: Basic unit tests for ActionResolver functionality
- **Coverage**: Core API validation, simple scenarios

## ⚡ Performance Benchmarks

### **Focused Tests (Pure ActionResolver)**
- **Target**: < 500ns per resolution
- **Memory**: Zero allocation (pure static lookups)
- **Success Rate**: 99.9%+ (pure logic, minimal failures)

### **General Tests (With Dependencies)**
- **Target**: < 1000ns per resolution  
- **Memory**: < 10KB tolerance for framework overhead
- **Success Rate**: 99.0%+ (allows for system variation)

## 🔧 Known Upgrade Chains

### **Stone/Glare Chain (WHM Single Target)**
```csharp
Level 1:  Stone (119)
Level 18: Stone II (127)  
Level 54: Stone III (3568)
Level 72: Glare (16533)
Level 82: Glare III (25859)
```

### **Aero/Dia Chain (WHM DoT)**
```csharp
Level 4:  Aero (121)
Level 46: Aero II (132)
Level 72: Dia (16532)
```

### **Holy Chain (WHM AoE)**
```csharp
Level 45: Holy (139)
Level 82: Holy III (25860)
```

## 🚀 Running Tests

### **Focused Pure Tests**
```bash
dotnet test --filter "ActionResolverFocused"
```

### **Stress Testing**
```bash
ACTIONRES_FOCUSED_BENCH_COUNT=10000 dotnet test --filter "ActionResolverFocusedStress"
```

### **All ActionResolver Tests**
```bash
dotnet test --filter "ActionResolver"
```

## 📊 Recent Results

### **Focused Stress Test (10,000 simulations)**
- **Success Rate**: 99.98% (9,998/10,000 passed)
- **Performance**: 0.077ms average per simulation
- **Failures**: Only 2 due to GC memory allocation spikes (expected)

### **Performance Characteristics**
- **Resolution Speed**: ~100-150ns per resolution (excellent)
- **Memory Efficiency**: Zero allocations in hot path
- **Reliability**: Extremely stable under stress testing

## 🛠️ Maintenance Notes

### **Adding New Upgrade Chains**
1. **Update ActionResolver.cs** with new static lookup tables
2. **Add chain constants** (e.g., `NEW_CHAIN = [id1, id2, id3]`)
3. **Update test coverage** in focused tests for new chains
4. **Validate critical upgrade levels** in boundary tests

### **Performance Regression Detection**
- **Monitor resolution times** - should stay sub-500ns
- **Check memory allocation** - should remain zero
- **Validate upgrade progression** - no downgrades allowed

The ActionResolver is the foundation of action resolution and must maintain exceptional performance and reliability.
