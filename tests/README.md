# ModernWrathCombo Testing

This directory contains all tests for the ModernWrathCombo project, organized by test type:

## Directory Structure

```
tests/
├── Unit/                      # Unit tests for individual components
│   ├── Core/                  # Core system tests (ActionResolver, GameState, etc.)
│   └── Jobs/WHM/              # Job-specific logic tests
├── Integration/               # End-to-end integration tests
├── Performance/               # Performance benchmarks and stress tests
└── ModernWrathCombo.Tests.csproj  # Test project configuration
```

## Test Categories

### Unit Tests (`tests/Unit/`)
- **Purpose**: Test individual components in isolation
- **Framework**: xUnit with FluentAssertions
- **Focus**: Fast execution, high coverage, isolated components
- **Examples**: 
  - `ActionResolverTests.cs` - Dictionary lookup behavior
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
  - `ActionResolverBenchmarks.cs` - Micro-benchmarks for hot paths

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
