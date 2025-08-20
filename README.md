# ModernActionCombo 🚀

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](LICENSE)
[![Performance](https://img.shields.io/badge/Performance-78%2F100-yellow.svg)](PERFORMANCE.md)
[![Status](https://img.shields.io/badge/Status-Backend%20Complete-green.svg)](DEV_JOURNAL.md)

> **Modern High-Performance Action Resolver for FFXIV**  
> Ultra-fast combo system with modern .NET 9 optimizations. Built from the ground up for performance and simplicity.

## What is ModernActionCombo?

**ModernActionCombo** is a clean-slate, ultra-high-performance action resolver for Final Fantasy XIV that consolidates combos and abilities onto single buttons with sub-50ns resolution times.

This is a **complete rewrite** focusing on:
- **Performance**: Sub-50ns action resolution with zero allocations
- **Simplicity**: Clean architecture that's easy to understand and extend  
- **Reliability**: Button mashing protection and comprehensive testing
- **Modern .NET**: Leveraging .NET 9's latest performance optimizations

### Thanks

This project was very much inspired by [WrathCombo team](https://github.com/PunishXIV/WrathCombo)'s WrathCombo. I wanted to see how far I could push a system like this.

### Project Structure
```
/ModernActionCombo/
├── src/                         # Streamlined implementation
│   ├── Core/                    # ActionResolver, GameState, IActionHandler
│   └── Jobs/WHM/               # WHM constants and combo logic
├── tests/                       # Comprehensive testing (Dalamud-free)
│   ├── Unit/                   # Component isolation tests
│   ├── Integration/            # End-to-end workflow tests
│   └── Performance/            # BenchmarkDotNet with 0-100 scoring
├── run-benchmarks.sh           # Quick performance validation
├── PERFORMANCE.md              # Performance guide and scoring
└── DEV_JOURNAL.md              # Complete development history
```

### Phase 3: Active
- [ ] UI development for configuration
- [ ] Dalamud plugin integration
- [ ] Live FFXIV testing
- [ ] Additional job implementations

### **Quick Benchmark Results** (5-10 seconds):
- **FastPath Resolution**: 17.43ns → 70/100 🟡
- **GameState Resolution**: 71.36ns → 70/100 🟡  
- **Batch Processing**: 203.91ns → 95/100 🟢
- **Overall Score**: 78/100 🟡 "Good! Meeting performance requirements!"

## �📜 License

This project is licensed under the AGPL v3 License - see the [LICENSE](LICENSE) file for details.
