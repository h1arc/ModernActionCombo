using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Tests.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for the ModernActionCombo system.
/// These tests validate our sub-50ns performance targets.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class CorePerformanceBenchmarks
{
    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public GameStateData CreateGameStateSnapshot()
    {
        return GameStateCache.CreateSnapshot();
    }
    
    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public GameStateData ConstructGameStateData()
    {
        return new GameStateData(24, 90, true, 12345, 2.5f);
    }
    
    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public uint WHMConstantsResolve()
    {
        return WHMConstants.ResolveActionForLevel(119, 90); // Stone → SingleTarget at level 90
    }
    
    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool GameStateConditionChecks()
    {
        var gameState = new GameStateData(24, 90, true, 12345, 1.0f);
        return gameState.CanUseAbility() && gameState.IsValidTarget();
    }
    
    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public uint GetDoTDebuff()
    {
        return WHMConstants.GetDoTDebuff(121); // Aero → debuff ID
    }
}
