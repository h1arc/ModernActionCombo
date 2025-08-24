# JobRotation Tests

Tests for job-specific rotation logic and combo management systems.

## 🎯 Component Overview

**JobRotation** manages job-specific ability rotations, combo tracking, and optimization logic for different FFXIV jobs.

### Key Features
- **Job-specific Logic**: Tailored rotation algorithms for each job class
- **Combo Tracking**: State management for multi-step ability combos
- **Priority Systems**: Intelligent ability prioritization based on situation
- **Performance Optimization**: Fast rotation decision making for real-time usage

## 🧪 Test Files

### **JobRotationTests.cs**
- **Purpose**: Core job rotation logic testing
- **Coverage**: Combo tracking, priority resolution, job-specific algorithms
- **Scope**: Individual job rotation validation and cross-job consistency

## 🎮 Job Coverage

### **Supported Jobs**
The rotation system supports multiple FFXIV job classes:
- **Tanks**: Warrior (WAR), Dark Knight (DRK), etc.
- **Healers**: White Mage (WHM), Scholar (SCH), etc.
- **DPS**: Black Mage (BLM), Summoner (SMN), etc.

### **Rotation Elements**
- **Basic Combos**: Multi-step ability sequences
- **Priority Systems**: Situation-based ability selection
- **Resource Management**: Job-specific resource optimization
- **Buff/Debuff Timing**: Optimal timing for temporary effects

## 🔧 Test Scenarios

### **Combo Validation**
- **Sequence Tracking**: Proper combo step progression
- **State Management**: Combo state persistence and reset
- **Error Handling**: Invalid combo transitions
- **Performance**: Fast combo state evaluation

### **Priority Resolution**
- **Situational Logic**: Context-dependent ability selection
- **Resource Optimization**: Efficient use of job resources
- **Cooldown Management**: Optimal timing for long cooldown abilities
- **Emergency Situations**: High-priority ability override logic

### **Job-specific Testing**
- **WHM Rotation**: Healing priority, DPS optimization
- **BLM Rotation**: Enochian management, spell casting optimization
- **WAR Rotation**: Stance management, combo optimization
- **Cross-job Consistency**: Shared systems across different jobs

## ⚡ Performance Requirements

### **Decision Speed**
- **Target**: Sub-microsecond rotation decisions
- **Real-time Usage**: Fast enough for live combat scenarios
- **Memory Efficiency**: Minimal allocation during rotation logic

### **State Management**
- **Combo Tracking**: Efficient combo state storage and retrieval
- **Resource Monitoring**: Low-overhead resource level tracking
- **Cooldown Integration**: Fast cooldown state queries

## 🚀 Running Tests

### **All Job Rotation Tests**
```bash
dotnet test --filter "JobRotation"
```

### **Specific Job Testing**
```bash
# When job-specific test files are added
dotnet test --filter "WHMRotation"
dotnet test --filter "BLMRotation"
```

## 📊 Rotation Algorithms

### **Priority-based Systems**
- **Weighted Scoring**: Numeric priority calculation for ability selection
- **Conditional Logic**: Situation-dependent ability availability
- **Fallback Chains**: Secondary options when primary abilities unavailable

### **State Machines**
- **Combo States**: Formal state machine for multi-step combos
- **Job Mechanics**: State tracking for job-specific mechanics
- **Transition Logic**: Valid state transitions and invalid state handling

## 🛠️ Integration Points

### **ActionResolver Integration**
- **Action Upgrades**: Using appropriate level-based action versions
- **Availability Checking**: Ensuring selected actions are available

### **OGCDResolver Integration**
- **OGCD Weaving**: Integrating off-global abilities into rotations
- **Priority Coordination**: Balancing GCD and OGCD priorities

### **GameStateCache Integration**
- **Resource Levels**: Current mana, health, job-specific resources
- **Buff/Debuff Status**: Active effects affecting rotation decisions
- **Target Information**: Target-dependent rotation adjustments

## 🔧 Development Guidelines

### **Adding New Jobs**
1. **Create Job Class**: Implement job-specific rotation logic
2. **Define Combos**: Establish multi-step ability sequences
3. **Priority System**: Implement situation-based ability selection
4. **Test Coverage**: Create comprehensive tests for new job

### **Performance Optimization**
- **Decision Trees**: Optimize ability selection algorithms
- **State Caching**: Cache frequently accessed rotation state
- **Memory Layout**: Optimize data structures for cache performance

### **Testing Strategy**
- **Scenario Coverage**: Test common and edge case scenarios
- **Performance Validation**: Ensure rotation decisions are fast enough
- **Cross-job Testing**: Validate shared systems work across all jobs

## 🛠️ Maintenance Notes

### **Rotation Updates**
When FFXIV game mechanics change:
1. **Update Algorithms**: Modify rotation logic for new mechanics
2. **Test Coverage**: Ensure tests cover new/changed functionality
3. **Performance Impact**: Validate changes don't degrade performance

### **Future Enhancements**
- **AI Integration**: Machine learning for rotation optimization
- **User Customization**: Configurable rotation preferences
- **Advanced Metrics**: Detailed rotation performance analysis

The JobRotation system is critical for optimal gameplay performance and must provide fast, accurate rotation decisions for real-time combat scenarios.
