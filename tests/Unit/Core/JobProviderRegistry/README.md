# JobProviderRegistry Tests

Tests for the JobProviderRegistry component, which handles automatic discovery and management of job-specific providers.

## 🎯 Component Overview

**JobProviderRegistry** is the central hub for managing all job-specific functionality, automatically discovering and registering providers with [JobCombo] attributes.

### Key Features
- **Automatic Discovery**: Scans assembly for [JobCombo] attributed classes
- **Provider Management**: Registers and manages IJobProvider implementations
- **Fast Resolution**: Ultra-optimized single-dispatch action resolution (<20ns)
- **Capability Detection**: Automatically detects Combo, Gauge, and Tracking support
- **State Management**: Handles job changes, level changes, duty/combat state changes
- **Multi-Interface Support**: Unified registry replacing JobComboRegistry, JobGaugeRegistry, JobTrackingDataRegistry

## 🧪 Test Files

### **JobProviderRegistryFocusedRandomizedTests.cs**
- **Purpose**: Comprehensive focused testing without complex dependencies
- **Coverage**: Provider discovery, job switching, capability detection, performance
- **Scenarios**: Random job configurations, state changes, error conditions

### **JobProviderRegistryFocusedStressTests.cs**
- **Environment Variable**: `JOBREGISTRY_BENCH_COUNT`
- **Default**: 100 simulations
- **Purpose**: Stress testing provider management and performance
- **Scaling**: Up to 100,000 iterations for extreme validation

## ⚡ Performance Benchmarks

### **Provider Lookup Performance**
- **Target**: < 1000ns per provider lookup
- **Memory**: < 10KB tolerance for framework overhead
- **Success Rate**: 99.0%+ (allows for complex reflection operations)

### **Resolution Performance**
- **Fast Resolver**: <20ns for action resolution (ultra-optimized single-dispatch)
- **Cache Invalidation**: Efficient cache rebuilding on job/config changes
- **Memory Efficiency**: Minimal allocation during hot path operations

## 🔧 Test Scenarios

### **Provider Discovery**
- **Initialization**: Safe multiple initialization calls
- **Registration**: Automatic discovery of [JobCombo] attributed classes
- **Capability Detection**: Combo, Gauge, Tracking capability identification
- **Provider Validation**: Ensures HasProvider/GetProvider consistency

### **Job Management**
- **Job Switching**: OnJobChanged with valid/invalid job IDs
- **Active Provider**: Proper active provider setting and clearing
- **Provider Lookup**: GetProvider/HasProvider performance and correctness
- **Job Names**: GetJobName for valid and invalid job IDs

### **State Management**
- **Level Changes**: OnLevelChanged with various level values
- **Duty State**: OnDutyStateChanged with in/out duty scenarios
- **Combat State**: OnCombatStateChanged with combat transitions
- **Multiple Changes**: Rapid state change sequences

### **Capability Testing**
- **Interface Support**: HasComboSupport, HasGaugeSupport, HasTrackingSupport
- **Provider Casting**: AsComboProvider, AsGaugeProvider, AsTrackingProvider
- **Display Info**: GetJobDisplayInfo consistency
- **Debug Information**: GetAllDebugInfo, GetGaugeDebugInfo

### **Error Handling**
- **Edge Cases**: null/invalid inputs, extreme job IDs
- **Exception Safety**: Methods should not crash on invalid input
- **State Recovery**: Proper behavior after errors

## 🚀 Running Tests

### **Focused Testing**
```bash
dotnet test --filter "JobProviderRegistryFocused"
```

### **Stress Testing**
```bash
JOBREGISTRY_BENCH_COUNT=10000 dotnet test --filter "JobProviderRegistryFocusedStress"
```

### **All JobProviderRegistry Tests**
```bash
dotnet test --filter "JobProviderRegistry"
```

## 📊 Key Responsibilities

### **Provider Discovery (Initialization)**
- **Assembly Scanning**: Find [JobCombo] attributed classes
- **Interface Validation**: Ensure classes implement IJobProvider
- **Registration**: Register providers by JobId
- **Capability Logging**: Log discovered capabilities per provider

### **Job Management**
- **Active Provider**: Manage currently active job provider
- **Fast Resolver Cache**: Ultra-optimized action resolution caching
- **Job Switching**: Handle job changes with proper cleanup
- **Provider Access**: Fast lookup of providers by job ID

### **Action Resolution (Hot Path)**
- **Ultra-Fast Resolution**: <20ns action resolution via cached single-dispatch
- **Config Awareness**: Cache invalidation on configuration changes
- **OGCD Integration**: Seamless OGCD weaving with GCD resolution
- **Grid Support**: Single and multi-grid combo evaluation

### **State Synchronization**
- **Level Changes**: Notify providers and invalidate caches
- **Duty State**: Handle duty entry/exit across all providers  
- **Combat State**: Manage combat transitions for all providers
- **Error Isolation**: Provider errors don't affect others

### **Multi-Registry Replacement**
- **Combo Processing**: Replaces JobComboRegistry functionality
- **Gauge Management**: Replaces JobGaugeRegistry functionality
- **Tracking Data**: Replaces JobTrackingDataRegistry functionality
- **Unified Interface**: Single registry for all job-related operations

## 🛠️ Maintenance Notes

### **Adding New Tests**
1. **Use realistic job IDs** from actual FFXIV jobs
2. **Test state isolation** - providers shouldn't affect each other
3. **Include performance tests** - registry is performance-critical
4. **Test error conditions** - invalid jobs, reflection failures

### **Performance Monitoring**
- **Provider lookup speed** - should stay sub-microsecond
- **Resolution performance** - <20ns for hot path
- **Memory allocation** - minimal allocation during lookups
- **Cache invalidation cost** - efficient rebuilding

### **Registry State Management**
- **Test Isolation**: Use reflection to reset static state between tests
- **Multiple Initialization**: Ensure safe repeated initialization
- **Provider Lifecycle**: Proper provider initialization and cleanup
- **Error Recovery**: Registry should handle provider errors gracefully

### **Integration Considerations**
- **GameStateCache Integration**: Providers depend on game state
- **Configuration Integration**: Cache invalidation on config changes
- **Action Resolution**: Integration with ActionResolver for upgrades
- **OGCD Resolution**: Integration with OGCDResolver for weaving

## 🔍 Architecture Notes

### **Single-Dispatch Optimization**
The registry implements ultra-fast action resolution using cached single-dispatch:
```csharp
// Ultra-fast path: <20ns resolution
if (_fastResolver != null && 
    gameState.JobId == _cachedJobId && 
    currentConfigVersion == _cachedConfigVersion)
{
    return _fastResolver(actionId, gameState);
}
```

### **Provider Composition**
Providers can implement multiple interfaces:
- **IComboProvider**: Combo grid evaluation
- **IGaugeProvider**: Job gauge management  
- **ITrackingProvider**: Buff/debuff tracking
- **IOGCDProvider**: OGCD suggestion and weaving

### **State Change Handling**
Efficient notification system for state changes:
- **Level Changes**: Cache invalidation + provider notification
- **Job Changes**: Active provider switching + cache rebuild
- **Combat/Duty**: Provider-specific state handling

The JobProviderRegistry is the central nervous system of the job system and must maintain exceptional performance while managing complex provider interactions.
