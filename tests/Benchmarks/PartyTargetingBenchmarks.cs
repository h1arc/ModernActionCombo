using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ModernActionCombo.Tests.Benchmarks;

/// <summary>
/// Benchmarks comparing different party member targeting approaches.
/// Tests both SIMD-optimized simple struct vs hot-pathed per-member cache lines.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[MarkdownExporter]
public unsafe class PartyTargetingBenchmarks
{
    private PartyMemberSimpleCache _simpleCache;
    private PartyMemberHotCache _hotCache;
    private PartyMemberHotPath* _memberHotPaths;
    private Random _random = new(42); // Deterministic for consistent benchmarks
    
    [GlobalSetup]
    public void Setup()
    {
        // Setup simple cache
        _simpleCache = new PartyMemberSimpleCache();
        _simpleCache.Initialize();
        
        // Setup hot cache
        _hotCache = new PartyMemberHotCache();
        _hotCache.Initialize();
        
        // Setup hot paths - allocate cache-line aligned memory
        _memberHotPaths = (PartyMemberHotPath*)NativeMemory.AlignedAlloc(8 * 64, 64);
        for (int i = 0; i < 8; i++)
        {
            _memberHotPaths[i] = new PartyMemberHotPath
            {
                MemberId = (uint)(1000 + i),
                HpPercentage = _random.NextSingle(),
                StatusFlags = (uint)(_random.Next(0, 2) == 0 ? 0x7 : 0x0), // Alive, in range, targetable
                HotPriority = (byte)i,
                JobRole = (byte)(_random.Next(0, 3)),
                DistanceFromSelf = _random.NextSingle() * 30f
            };
        }
        
        // Populate both caches with realistic party data
        PopulateTestData();
    }
    
    [GlobalCleanup]
    public void Cleanup()
    {
        if (_memberHotPaths != null)
        {
            NativeMemory.AlignedFree(_memberHotPaths);
        }
    }
    
    private void PopulateTestData()
    {
        // Create realistic party scenarios
        var scenarios = new[]
        {
            // Scenario 1: Tank low, others healthy
            new float[] { 0.25f, 0.85f, 0.92f, 0.88f, 0.95f, 0.77f, 0.83f, 0.91f },
            // Scenario 2: Multiple members need healing
            new float[] { 0.45f, 0.32f, 0.78f, 0.23f, 0.67f, 0.89f, 0.44f, 0.55f },
            // Scenario 3: Everyone healthy
            new float[] { 0.98f, 0.95f, 0.92f, 0.88f, 0.94f, 0.97f, 0.93f, 0.96f },
            // Scenario 4: Emergency - multiple critical
            new float[] { 0.15f, 0.08f, 0.22f, 0.18f, 0.45f, 0.12f, 0.33f, 0.28f }
        };
        
        var scenario = scenarios[_random.Next(scenarios.Length)];
        
        // Update both caches with same data
        _simpleCache.UpdatePartyData(scenario);
        _hotCache.UpdatePartyData(scenario);
        
        // Update hot paths
        for (int i = 0; i < 8; i++)
        {
            _memberHotPaths[i].HpPercentage = scenario[i];
            _memberHotPaths[i].StatusFlags = scenario[i] > 0.0f ? 0x7u : 0x0u; // Alive if HP > 0
        }
    }
    
    [Benchmark(Baseline = true)]
    public uint SimpleCache_GetBestTarget()
    {
        return _simpleCache.GetBestHealTarget(0.95f);
    }
    
    [Benchmark]
    public uint HotCache_GetBestTarget()
    {
        return _hotCache.GetBestHealTarget(0.95f);
    }
    
    [Benchmark]
    public uint HotPaths_GetBestTarget()
    {
        return GetBestTargetFromHotPaths(0.95f);
    }
    
    [Benchmark]
    public void SimpleCache_SortOperation()
    {
        _simpleCache.SortByHpPercentage();
    }
    
    [Benchmark]
    public void HotCache_SortOperation()
    {
        _hotCache.SortByHpAndPriority();
    }
    
    [Benchmark]
    public void HotPaths_SortOperation()
    {
        SortHotPathsByHp();
    }
    
    [Benchmark]
    public uint SimpleCache_EmergencyTarget()
    {
        return _simpleCache.GetEmergencyHealTarget();
    }
    
    [Benchmark]
    public uint HotPaths_EmergencyTarget()
    {
        return GetEmergencyTargetFromHotPaths();
    }
    
    [Benchmark]
    public void SimpleCache_MemoryAccess()
    {
        // Test memory access patterns
        for (int i = 0; i < 100; i++)
        {
            _simpleCache.GetMemberHpPercent(i % 8);
        }
    }
    
    [Benchmark]
    public void HotPaths_MemoryAccess()
    {
        // Test memory access patterns
        for (int i = 0; i < 100; i++)
        {
            var hp = _memberHotPaths[i % 8].HpPercentage;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetBestTargetFromHotPaths(float hpThreshold)
    {
        uint bestTarget = 0;
        float lowestHp = 1.0f;
        
        for (int i = 0; i < 8; i++)
        {
            var member = &_memberHotPaths[i];
            if (member->IsValidHealTarget(hpThreshold) && member->HpPercentage < lowestHp)
            {
                lowestHp = member->HpPercentage;
                bestTarget = member->MemberId;
            }
        }
        
        return bestTarget;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetEmergencyTargetFromHotPaths()
    {
        // Tanks first
        for (int i = 0; i < 8; i++)
        {
            var member = &_memberHotPaths[i];
            if (member->NeedsUrgentHealing() && member->JobRole == 0)
            {
                return member->MemberId;
            }
        }
        
        // Any urgent target
        for (int i = 0; i < 8; i++)
        {
            var member = &_memberHotPaths[i];
            if (member->NeedsUrgentHealing())
            {
                return member->MemberId;
            }
        }
        
        return 0;
    }
    
    private void SortHotPathsByHp()
    {
        // Simple insertion sort for hot paths
        for (int i = 1; i < 8; i++)
        {
            var temp = _memberHotPaths[i];
            int j = i - 1;
            
            while (j >= 0 && _memberHotPaths[j].HpPercentage > temp.HpPercentage)
            {
                _memberHotPaths[j + 1] = _memberHotPaths[j];
                j--;
            }
            _memberHotPaths[j + 1] = temp;
        }
    }
}

/// <summary>
/// Simple cache approach - SIMD optimized but compact structure.
/// This is the "first idea" - clean and straightforward.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PartyMemberSimpleCache
{
    private const int MaxPartySize = 8;
    
    // Simple fixed arrays - all data together for cache efficiency
    private fixed uint _memberIds[MaxPartySize];
    private fixed float _hpPercentages[MaxPartySize];
    private fixed uint _statusFlags[MaxPartySize];
    private fixed byte _sortedIndices[MaxPartySize];
    private byte _memberCount;
    private long _lastSortTicks;
    
    // Status flags
    private const uint AliveFlag = 1u << 0;
    private const uint InRangeFlag = 1u << 1;
    private const uint TargetableFlag = 1u << 2;
    
    public void Initialize()
    {
        _memberCount = 8;
        _lastSortTicks = 0;
        
        fixed (uint* ids = _memberIds)
        fixed (byte* indices = _sortedIndices)
        {
            for (int i = 0; i < MaxPartySize; i++)
            {
                ids[i] = (uint)(1000 + i);
                indices[i] = (byte)i;
            }
        }
    }
    
    public void UpdatePartyData(float[] hpPercentages)
    {
        fixed (float* hp = _hpPercentages)
        fixed (uint* status = _statusFlags)
        {
            for (int i = 0; i < Math.Min(hpPercentages.Length, MaxPartySize); i++)
            {
                hp[i] = hpPercentages[i];
                status[i] = hpPercentages[i] > 0.0f ? (AliveFlag | InRangeFlag | TargetableFlag) : 0u;
            }
        }
        _lastSortTicks = 0; // Force resort
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SortByHpPercentage()
    {
        var now = Environment.TickCount64;
        if (now - _lastSortTicks < 30) return; // 30ms threshold
        
        fixed (float* hp = _hpPercentages)
        fixed (uint* status = _statusFlags)
        fixed (byte* indices = _sortedIndices)
        {
            // SIMD optimization for alive check when possible
            if (Vector256.IsHardwareAccelerated)
            {
                var statusVec = Vector256.Load(status);
                var aliveMask = Vector256.BitwiseAnd(statusVec, Vector256.Create(AliveFlag));
                
                // Could add more SIMD operations here for HP comparison
            }
            
            // Simple insertion sort (optimal for small arrays)
            for (byte i = 1; i < _memberCount; i++)
            {
                var currentIdx = indices[i];
                byte j = i;
                
                while (j > 0 && ShouldSwapSimple(indices[j - 1], currentIdx, hp, status))
                {
                    indices[j] = indices[j - 1];
                    j--;
                }
                indices[j] = currentIdx;
            }
        }
        
        _lastSortTicks = now;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldSwapSimple(byte aIdx, byte bIdx, float* hp, uint* status)
    {
        var aAlive = (status[aIdx] & AliveFlag) != 0;
        var bAlive = (status[bIdx] & AliveFlag) != 0;
        
        if (aAlive != bAlive) return bAlive; // Alive members first
        if (!aAlive) return false; // Both dead, keep order
        
        return hp[bIdx] < hp[aIdx]; // Lower HP first
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetBestHealTarget(float hpThreshold)
    {
        SortByHpPercentage();
        
        fixed (byte* indices = _sortedIndices)
        fixed (float* hp = _hpPercentages)
        fixed (uint* ids = _memberIds)
        fixed (uint* status = _statusFlags)
        {
            // Hot path: check first member
            if (_memberCount > 0)
            {
                var bestIdx = indices[0];
                if ((status[bestIdx] & (AliveFlag | InRangeFlag)) == (AliveFlag | InRangeFlag) &&
                    hp[bestIdx] < hpThreshold)
                {
                    return ids[bestIdx];
                }
            }
        }
        
        return 0; // No valid target or fallback to self
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetEmergencyHealTarget()
    {
        SortByHpPercentage();
        
        fixed (byte* indices = _sortedIndices)
        fixed (float* hp = _hpPercentages)
        fixed (uint* ids = _memberIds)
        fixed (uint* status = _statusFlags)
        {
            // Emergency: anyone under 30%
            for (int i = 0; i < _memberCount; i++)
            {
                var idx = indices[i];
                if ((status[idx] & AliveFlag) != 0 && hp[idx] < 0.3f)
                {
                    return ids[idx];
                }
            }
        }
        
        return 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetMemberHpPercent(int index)
    {
        if (index < 0 || index >= MaxPartySize) return 0f;
        
        fixed (float* hp = _hpPercentages)
        {
            return hp[index];
        }
    }
}

/// <summary>
/// Hot cache approach - more complex but potentially faster for frequent access.
/// This is the "second idea" with cache-line optimizations.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PartyMemberHotCache
{
    private const int MaxPartySize = 8;
    
    // More complex structure with hot/cold data separation
    private fixed uint _memberIds[MaxPartySize];
    private fixed float _hpPercentages[MaxPartySize];
    private fixed uint _statusFlags[MaxPartySize];
    private fixed byte _hotPathPriority[MaxPartySize];
    private fixed byte _jobRoles[MaxPartySize];
    private fixed float _distances[MaxPartySize];
    private fixed byte _sortedIndices[MaxPartySize];
    private byte _validMemberCount;
    private long _lastSortTicks;
    
    // Status flags
    private const uint AliveFlag = 1u << 0;
    private const uint InRangeFlag = 1u << 1;
    private const uint TargetableFlag = 1u << 2;
    
    public void Initialize()
    {
        _validMemberCount = 8;
        _lastSortTicks = 0;
        
        fixed (uint* ids = _memberIds)
        fixed (byte* indices = _sortedIndices)
        fixed (byte* priority = _hotPathPriority)
        fixed (byte* roles = _jobRoles)
        {
            for (int i = 0; i < MaxPartySize; i++)
            {
                ids[i] = (uint)(1000 + i);
                indices[i] = (byte)i;
                priority[i] = (byte)i;
                roles[i] = (byte)(i % 3); // Mix of roles
            }
        }
    }
    
    public void UpdatePartyData(float[] hpPercentages)
    {
        fixed (float* hp = _hpPercentages)
        fixed (uint* status = _statusFlags)
        {
            for (int i = 0; i < Math.Min(hpPercentages.Length, MaxPartySize); i++)
            {
                hp[i] = hpPercentages[i];
                status[i] = hpPercentages[i] > 0.0f ? (AliveFlag | InRangeFlag | TargetableFlag) : 0u;
            }
        }
        _lastSortTicks = 0; // Force resort
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SortByHpAndPriority()
    {
        var now = Environment.TickCount64;
        if (now - _lastSortTicks < 30) return;
        
        fixed (float* hp = _hpPercentages)
        fixed (uint* status = _statusFlags)
        fixed (byte* priority = _hotPathPriority)
        fixed (byte* indices = _sortedIndices)
        {
            // More complex sorting with multiple criteria
            for (byte i = 1; i < _validMemberCount; i++)
            {
                var currentIdx = indices[i];
                byte j = i;
                
                while (j > 0 && ShouldSwapComplex(indices[j - 1], currentIdx, hp, status, priority))
                {
                    indices[j] = indices[j - 1];
                    j--;
                }
                indices[j] = currentIdx;
            }
        }
        
        _lastSortTicks = now;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldSwapComplex(byte aIdx, byte bIdx, float* hp, uint* status, byte* priority)
    {
        var aAlive = (status[aIdx] & AliveFlag) != 0;
        var bAlive = (status[bIdx] & AliveFlag) != 0;
        
        if (aAlive != bAlive) return bAlive;
        if (!aAlive) return false;
        
        var aHp = hp[aIdx];
        var bHp = hp[bIdx];
        
        if (Math.Abs(aHp - bHp) > 0.01f) return bHp < aHp;
        
        // Tie-breaker: hot priority
        return priority[bIdx] < priority[aIdx];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetBestHealTarget(float hpThreshold)
    {
        SortByHpAndPriority();
        
        fixed (byte* indices = _sortedIndices)
        fixed (float* hp = _hpPercentages)
        fixed (uint* ids = _memberIds)
        fixed (uint* status = _statusFlags)
        {
            if (_validMemberCount > 0)
            {
                var bestIdx = indices[0];
                if ((status[bestIdx] & (AliveFlag | InRangeFlag)) == (AliveFlag | InRangeFlag) &&
                    hp[bestIdx] < hpThreshold)
                {
                    return ids[bestIdx];
                }
            }
        }
        
        return 0;
    }
}

/// <summary>
/// Per-member hot path structure - cache-line aligned.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 64)]
public unsafe struct PartyMemberHotPath
{
    public uint MemberId;
    public float HpPercentage;
    public uint StatusFlags;
    public byte HotPriority;
    public byte JobRole;
    public float DistanceFromSelf;
    public float LastKnownHp;
    
    private fixed byte _padding[40]; // Cache line padding
    
    private const uint AliveFlag = 1u << 0;
    private const uint InRangeFlag = 1u << 1;
    private const uint TargetableFlag = 1u << 2;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidHealTarget(float hpThreshold) =>
        (StatusFlags & (AliveFlag | InRangeFlag)) == (AliveFlag | InRangeFlag) &&
        HpPercentage < hpThreshold;
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsUrgentHealing() => 
        IsValidHealTarget(1.0f) && HpPercentage < 0.3f;
}
