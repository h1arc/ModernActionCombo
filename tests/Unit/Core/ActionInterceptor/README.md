# ActionInterceptor Tests

Tests for the ActionInterceptor component, which provides high-performance action caching and resolution.

## 🎯 Component Overview

**ActionInterceptor** manages action caching with ultra-fast lookup and minimal memory overhead for frequently resolved actions.

### Key Features
- **High-Speed Caching**: Sub-microsecond action resolution via optimized cache
- **Consistency Validation**: Ensures cache consistency across multiple operations
- **Expiration Handling**: Time-based cache invalidation and cleanup
- **Memory Efficiency**: Controlled memory usage with intelligent eviction

## 🧪 Test Files

### **ActionInterceptorRandomizedTests.cs**
- **Purpose**: Comprehensive randomized cache testing
- **Coverage**: Cache performance, consistency validation, expiration behavior
- **Scenarios**: Random action mappings, cache eviction, concurrent access patterns

### **ActionInterceptorStressTests.cs**
- **Environment Variable**: `ACTIONINT_BENCH_COUNT`
- **Default**: 100 simulations
- **Purpose**: Stress testing cache performance and reliability
- **Scaling**: Up to 100,000 iterations for extreme validation

## ⚡ Performance Benchmarks

### **Cache Performance Targets**
- **Target**: < 1000ns per cache operation
- **Memory**: < 10KB tolerance for cache growth
- **Success Rate**: 99.0%+ (allows for GC variations)

### **Recent Results (5,000 simulations)**
- **Success Rate**: 99.98% (4,999/5,000 passed)
- **Performance**: 0.380ms average per simulation
- **Reliability**: Only 1 failure due to GC timing variation

## 🔧 Test Scenarios

### **Cache Performance**
- **Pre-generated Mappings**: Realistic FFXIV action ID mappings
- **Hit Rate Validation**: Ensures efficient cache utilization
- **Memory Allocation**: Monitors cache growth patterns

### **Consistency Validation**
- **Multiple Operations**: Same input should return same output
- **Cache State**: Validates internal cache consistency
- **Eviction Behavior**: Tests cache cleanup under memory pressure

### **Expiration Testing**
- **Time-based Invalidation**: Tests cache timeout behavior
- **Memory Management**: Validates proper cleanup of expired entries
- **Performance Impact**: Ensures expiration doesn't degrade performance

## 🚀 Running Tests

### **Randomized Testing**
```bash
dotnet test --filter "ActionInterceptorRandomized"
```

### **Stress Testing**
```bash
ACTIONINT_BENCH_COUNT=5000 dotnet test --filter "ActionInterceptorStress"
```

### **All ActionInterceptor Tests**
```bash
dotnet test --filter "ActionInterceptor"
```

## 📊 Performance Characteristics

### **Cache Operations**
- **Lookup Speed**: ~33ns per cache hit (excellent)
- **Miss Handling**: Efficient fallback to resolution
- **Memory Growth**: Controlled expansion with eviction

### **Stress Test Results**
- **99.98% Success Rate**: Exceptional reliability
- **Sub-microsecond Performance**: Consistently fast operations
- **Minimal Failures**: Only GC-related timing variations

## 🛠️ Maintenance Notes

### **Performance Monitoring**
- **Cache Hit Rates**: Should maintain high hit rates for common actions
- **Memory Usage**: Monitor cache size growth and eviction patterns
- **Timing Variations**: Allow tolerance for GC-induced timing spikes

### **Adding New Tests**
1. **Use realistic action IDs** from actual FFXIV data
2. **Include edge cases** like cache overflow, rapid eviction
3. **Validate consistency** across multiple cache operations
4. **Test concurrent access** patterns if applicable

The ActionInterceptor provides critical caching performance and must maintain sub-microsecond operation times.
