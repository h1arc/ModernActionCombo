# GameStateCache Tests

Tests for the GameStateCache component and related game state management utilities.

## 🎯 Component Overview

**GameStateCache** provides centralized game state management with high-performance caching for frequently accessed game data.

### Key Features
- **Centralized State**: Single source of truth for game state data
- **High-Performance Access**: Optimized data structures for fast state queries
- **State Synchronization**: Reliable state updates and change detection
- **Memory Efficiency**: Compact state representation with minimal overhead

## 🧪 Test Files

### **GameStateCacheStub.cs**
- **Purpose**: Stub implementation for testing other components
- **Usage**: Provides controlled game state for isolated component testing
- **Benefits**: Eliminates dependencies when testing components that need game state

## 🔧 GameState Management

### **State Categories**
- **Player State**: Level, job, position, health, mana
- **Party State**: Party member information, roles, status effects
- **Combat State**: In combat status, target information, cooldowns
- **Environmental State**: Zone information, position data, range calculations

### **Performance Requirements**
- **Access Speed**: Sub-microsecond state access for hot paths
- **Update Efficiency**: Minimal overhead for state changes
- **Memory Usage**: Compact representation of game state data

## 🚀 Integration with Other Components

### **SmartTargeting Integration**
- **Party Data**: Provides party member information for targeting
- **Range Calculation**: Position data for ability range validation
- **Status Effects**: Health and status information for priority calculation

### **ActionResolver Integration**
- **Player Level**: Level-based action upgrade resolution
- **Job Information**: Job-specific action availability

### **OGCDResolver Integration**
- **Combat State**: In-combat status for OGCD prioritization
- **Cooldown Tracking**: Global and ability-specific cooldown states
- **Target Information**: Current target for context-dependent OGCD usage

## 🛠️ Testing Strategy

### **Stub Usage**
The GameStateCacheStub allows other components to be tested in isolation:
- **Controlled State**: Predictable game state for reproducible tests
- **No Side Effects**: Changes don't affect other test components
- **Fast Execution**: No real game state overhead during testing

### **State Scenarios**
- **Minimal State**: Basic required state for component functionality
- **Complex Scenarios**: Rich game state for advanced testing
- **Edge Cases**: Unusual or boundary game state conditions

## 🚀 Running Tests

### **Component Integration**
Most GameStateCache testing happens through integration with other components:
```bash
# Tests that use GameStateCache
dotnet test --filter "SmartTargeting"
dotnet test --filter "OGCDResolver"
```

### **Stub Validation**
```bash
# Tests that specifically use the stub
dotnet test --filter "GameStateCache"
```

## 📊 Performance Considerations

### **Access Patterns**
- **Hot Path Access**: Frequently accessed state should be optimized
- **Batch Updates**: Efficient bulk state updates
- **Change Detection**: Fast identification of state changes

### **Memory Layout**
- **Cache Friendly**: Data structures optimized for CPU cache
- **Minimal Overhead**: Compact state representation
- **Alignment**: Proper data alignment for SIMD operations

## 🛠️ Maintenance Notes

### **Stub Maintenance**
- **Keep Simple**: Stub should provide minimal required functionality
- **Match Interface**: Stub behavior should match real GameStateCache
- **Test Coverage**: Ensure stub covers all component testing needs

### **State Evolution**
As game state requirements change:
1. **Update Stub**: Ensure stub supports new state requirements
2. **Performance Impact**: Monitor impact of new state on performance
3. **Integration Testing**: Validate new state with all dependent components

### **Future Enhancements**
- **State Validation**: Add comprehensive state validation tests
- **Performance Benchmarks**: Establish baseline performance metrics
- **Concurrency Testing**: If applicable, test concurrent state access

The GameStateCache is foundational to the entire system and must provide reliable, high-performance access to game state for all dependent components.
