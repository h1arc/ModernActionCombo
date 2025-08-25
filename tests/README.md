# ModernActionCombo Testing

This directory is prepared for comprehensive testing of the ModernActionCombo system.

## Current Status

The test infrastructure has been cleaned up and simplified. We'll be building comprehensive tests as the architecture stabilizes.

## Future Testing Plans

### Unit Tests
- **Core Components**: GameStateCache, ComboBase, WHMHelper
- **Job Combos**: WHM combo logic, action resolution  
- **Performance**: SIMD operations, cache efficiency

### Integration Tests
- **Action Processing**: End-to-end combo execution
- **GameStateCache**: Real-world state updates
- **Plugin Integration**: ActionInterceptor workflows

### Performance Tests
- **Benchmarks**: BenchmarkDotNet for precise measurements
- **Stress Tests**: High-frequency action processing
- **Memory Tests**: SIMD cache performance

## Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Adding New Tests

When adding tests, follow these patterns:
- Unit tests in `Unit/` subdirectories
- Integration tests in `Integration/`
- Performance tests in `Performance/`
- Use FluentAssertions for readable test assertions
- Use BenchmarkDotNet for performance measurements
- **Focus**: Fast execution, high coverage, isolated components
- **Examples**: 
  - `WHMConstantsTests.cs` - Action constants and helper chains
  - `GameStateTests.cs` - Struct validation and helper methods
  - `WHMBasicComboTests.cs` - Combo logic with mocked dependencies

### Integration Tests (`tests/Integration/`)
- **Purpose**: Test complete workflows and component interactions
- **Framework**: xUnit with FluentAssertions  
- **Focus**: Real scenarios, end-to-end validation
- **Examples**:
  - `ActionResolutionIntegrationTests.cs` - Complete WHM rotation scenarios

### Performance Tests (`tests/Performance/`)
- **Purpose**: Validate performance targets and identify bottlenecks
- **Framework**: BenchmarkDotNet
- **Target**: <50ns action resolution time
- **Examples**:
  - `GameStateCacheBenchmarks.cs` - Micro-benchmarks for hot paths

## Running Tests

### All Tests
```bash
cd tests/
dotnet test
```

### Unit Tests Only
```bash
cd tests/
dotnet test --filter "Category=Unit"
```

### Performance Benchmarks
```bash
cd tests/
dotnet run --project . --configuration Release -f net9.0 -- --job short
```

### With Coverage
```bash
cd tests/
dotnet test --collect:"XPlat Code Coverage"
```

## Test Standards

### Naming Conventions
- Test classes: `{ComponentName}Tests.cs`
- Test methods: `{Method}_{Scenario}_{ExpectedResult}`
- Example: `Resolve_WithNoHandler_ReturnsOriginalAction`

### Assertions
- Use FluentAssertions for readable test code
- Prefer `result.Should().Be(expected)` over `Assert.Equal`
- Include descriptive failure messages: `result.Should().Be(expected, "because reason")`

### Performance Tests
- All hot path methods must have benchmark coverage
- Target: <50ns for action resolution
- Use `[MemoryDiagnoser]` to track allocations (should be zero)
- Test realistic scenarios, not just best/worst case

### Test Data
- Use `stackalloc` for test data when possible (zero allocation)
- Create helper methods for common test scenarios
- Keep test data focused and minimal

## Dependencies

- **xUnit**: Primary testing framework
- **FluentAssertions**: More readable assertions
- **BenchmarkDotNet**: Performance testing
- **coverlet.collector**: Code coverage collection

## CI/CD Integration

These tests are designed to run in CI/CD pipelines:
- Fast unit tests run on every commit
- Integration tests run on PR/merge
- Performance tests run nightly with regression detection
