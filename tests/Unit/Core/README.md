# Core Component Testing Structure

This directory contains organized tests for all core components of the ModernActionCombo system.

## 📁 Directory Structure

```
Core/
├── ActionInterceptor/     # Action cache performance and consistency tests
├── ActionResolver/        # Action upgrade chain resolution tests
├── GameStateCache/        # Game state management and caching tests
├── JobRotation/          # Job-specific rotation logic tests
├── OGCDResolver/         # Off-Global Cooldown resolution tests
└── SmartTargeting/       # Smart party member targeting tests
```

## 🧪 Test Categories

### **Randomized Tests**
- **Purpose**: Discover edge cases through random scenario generation
- **Pattern**: `*RandomizedTests.cs`
- **Characteristics**: Deterministic seeding, comprehensive scenario coverage

### **Stress Tests**
- **Purpose**: Validate performance and reliability under load
- **Pattern**: `*StressTests.cs`
- **Environment Variables**: Control test intensity (e.g., `ACTIONRES_BENCH_COUNT`)

### **Focused Tests**
- **Purpose**: Test specific components in isolation without dependencies
- **Pattern**: `*FocusedRandomizedTests.cs`
- **Benefits**: Pure component testing, no side effects

### **Integration Tests**
- **Purpose**: Test component interactions and end-to-end scenarios
- **Pattern**: `*IntegrationTests.cs`
- **Scope**: Cross-component workflows

### **Advanced Tests**
- **Purpose**: Complex scenario testing with realistic game conditions
- **Pattern**: `*AdvancedTests.cs`
- **Coverage**: Edge cases, triage scenarios, priority handling

## 🎯 Testing Standards

### **Performance Benchmarks**
- **Sub-microsecond Operations**: Core hotpath functions (< 1000ns)
- **Zero Allocation**: Critical performance paths should avoid GC pressure
- **99%+ Success Rates**: Randomized tests should achieve exceptional reliability

### **Environment Variable Control**
```bash
# CI/CD (fast validation)
export COMPONENT_BENCH_COUNT=100

# Development (moderate testing)  
export COMPONENT_BENCH_COUNT=1000

# Performance validation (thorough)
export COMPONENT_BENCH_COUNT=10000

# Stress testing (extreme)
export COMPONENT_BENCH_COUNT=100000
```

### **Naming Conventions**
- **Test Methods**: `ComponentName_Scenario_ExpectedBehavior`
- **Simulation Methods**: `RunComponentSimulations(int simulationCount)`
- **Environment Variables**: `COMPONENT_BENCH_COUNT`

## 🚀 Running Tests

### **All Core Tests**
```bash
dotnet test tests/ --filter "FullyQualifiedName~Core"
```

### **Specific Component**
```bash
dotnet test tests/ --filter "FullyQualifiedName~ActionResolver"
```

### **Stress Testing**
```bash
ACTIONRES_BENCH_COUNT=10000 dotnet test tests/ --filter "ActionResolverStress"
```

### **Focused Testing** (No Dependencies)
```bash
dotnet test tests/ --filter "Focused"
```

## 📊 Success Rate Targets

| Test Type | Target Success Rate | Acceptable Failures |
|-----------|-------------------|-------------------|
| Randomized | 99.5%+ | < 0.5% (GC spikes) |
| Stress | 99.0%+ | < 1.0% (system variation) |
| Focused | 99.9%+ | < 0.1% (pure logic) |
| Integration | 95.0%+ | < 5.0% (complex interactions) |

## 🔧 Maintenance Guidelines

### **Adding New Tests**
1. **Choose appropriate folder** based on primary component
2. **Follow naming conventions** for discoverability
3. **Include environment variable control** for stress tests
4. **Add deterministic seeding** for reproducible randomized tests

### **Performance Regression Detection**
- **Baseline measurements** in test comments
- **Threshold validation** with meaningful error messages
- **Memory allocation monitoring** for zero-allocation paths

### **Cross-Component Dependencies**
- **Prefer focused tests** for pure component logic
- **Use integration tests** for component interactions
- **Stub dependencies** when testing specific components

This organization ensures maintainable, discoverable, and reliable testing for all core components.
