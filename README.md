# ModernActionCombo 🚀

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](LICENSE)
[![Performance](https://img.shields.io/badge/Performance-78%2F100-yellow.svg)](PERFORMANCE.md)
[![Status](https://img.shields.io/badge/Status-Backend%20Complete-green.svg)](DEV_JOURNAL.md)

> **Modern High-Performance Action Resolver for FFXIV**  
> Yet another combo plugin with modern .NET 9 optimizations.

This is a **complete rewrite** focusing on:
- **Performance**: Sub-ms action resolution with zero allocations
- **Simplicity**: Clean architecture that's easy to understand and extend
- **Modern .NET**: Leveraging .NET 9's latest performance optimizations

### Project Structure (to be updated)
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

### Phase 3: Active (to be updated)
- [ ] UI development for configuration
- [ ] Dalamud plugin integration
- [ ] Live FFXIV testing
- [ ] Additional job implementations

## �📜 License

This project is licensed under the AGPL v3 License - see the [LICENSE](LICENSE) file for details.


This project was inspired by [WrathCombo team](https://github.com/PunishXIV/WrathCombo)'s WrathCombo plus other plugins like it.
