using System.Runtime.CompilerServices;
using ModernWrathCombo.Core.Data;

namespace ModernWrathCombo.Jobs.WHM;

/// <summary>
/// WHM action constants and helpers.
/// Now uses the centralized GameStateCache instead of local caching.
/// </summary>
public static class WHMConstants
{
    #region Raw Action IDs (Performance-optimized storage)
    // Store as const arrays for action resolution
    private static readonly uint[] SingleTargetIds = [119, 127, 3568, 16533, 25859]; // Stone, Stone2, Stone3, Glare, Glare3 (no Glare4 - requires Sacred Sight buff)
    private static readonly byte[] SingleTargetLevels = [1, 2, 18, 64, 72];
    
    private static readonly uint[] DoTIds = [121, 132, 16532]; // Aero, Aero2, Dia
    private static readonly byte[] DoTLevels = [4, 46, 72];
    
    private static readonly uint[] AoEIds = [139, 25860]; // Holy, Holy3
    private static readonly byte[] AoELevels = [45, 82];
    
    // Direct constants for most-used actions
    public const uint Stone3 = 3568;
    public const uint Glare3 = 25859;
    public const uint Glare4 = 37009;    // Only usable with Sacred Sight buff
    public const uint Dia = 16532;
    public const uint Holy = 139;
    public const uint AfflatusMisery = 16535;
    public const uint PresenceOfMind = 136;
    #endregion

    #region GameStateCache Integration
    /// <summary>Gets the optimal single-target action for current player level from GameStateCache.</summary>
    public static uint SingleTarget => GetOptimalSingleTargetFallback(GameStateCache.Level);
    
    /// <summary>Gets the optimal DoT action for current player level from GameStateCache.</summary>
    public static uint DoT => GetOptimalDoTFallback(GameStateCache.Level);
    
    /// <summary>Gets the optimal AoE action for current player level from GameStateCache.</summary>
    public static uint AoE => GetOptimalAoEFallback(GameStateCache.Level);
    #endregion

    #region Action Resolution (Fallback Implementation)
    // Used directly by GameStateCache integration
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetOptimalSingleTargetFallback(uint level)
    {
        // Reverse iteration for best action at current level
        for (int i = SingleTargetLevels.Length - 1; i >= 0; i--)
        {
            if (level >= SingleTargetLevels[i])
                return SingleTargetIds[i];
        }
        return SingleTargetIds[0]; // Default to Stone
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetOptimalDoTFallback(uint level)
    {
        if (level < 4) return 0; // No DoT available
        
        for (int i = DoTLevels.Length - 1; i >= 0; i--)
        {
            if (level >= DoTLevels[i])
                return DoTIds[i];
        }
        return 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetOptimalAoEFallback(uint level)
    {
        if (level < 45) return 0; // No AoE available
        
        for (int i = AoELevels.Length - 1; i >= 0; i--)
        {
            if (level >= AoELevels[i])
                return AoEIds[i];
        }
        return 0;
    }
    #endregion

    #region Action Resolver
    /// <summary>
    /// Resolves an action to the best available version for current level.
    /// Now delegates to GameStateCache instead of maintaining local cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ResolveAction(uint baseActionId)
    {
        return baseActionId switch
        {
            119 or 127 or 3568 or 16533 or 25859 => SingleTarget, // Stone variants, Glare variants
            121 or 132 or 16532 => DoT, // Aero variants, Dia
            139 or 25860 => AoE, // Holy variants
            _ => baseActionId // Unknown action - return as-is
        };
    }
    
    /// <summary>
    /// Gets all possible action IDs that should be intercepted for WHM single-target DPS.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] GetAllSingleTargetActions() => SingleTargetIds;
    #endregion

    #region Utility Methods
    /// <summary>Check if job is WHM/CNJ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJob(uint jobId) => jobId == 24u || jobId == 6u; // WHM (24) or CNJ (6)
    
    /// <summary>
    /// Ultra-streamlined rotation logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAction(bool inCombat, bool hasTarget, bool isAoE = false)
    {
        // Branch-free: return 0 if not (inCombat AND hasTarget), else return action
        var shouldAct = inCombat & hasTarget;
        var action = isAoE ? AoE : SingleTarget;
        
        return shouldAct ? action : 0u;
    }
    
    /// <summary>Gets the DoT debuff ID for a given DoT action.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetDoTDebuff(uint dotAction) => dotAction switch
    {
        16532 => 1871, // Dia
        132 => 144,    // Aero2  
        121 => 143,    // Aero
        _ => 0
    };
    #endregion
    
    #region Buff Constants
    public const ushort PresenceOfMindBuff = 157;
    public const ushort SacredSightBuff = 3879;
    #endregion
}
