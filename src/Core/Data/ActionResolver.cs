using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Elementary action resolution system.
/// Maps base actions to their level-appropriate upgraded versions.
/// Uses static lookup tables for maximum performance.
/// </summary>
public static class ActionResolver
{
    #region Action Upgrade Tables
    
    // Stone -> Stone II -> Stone III -> Glare -> Glare III (Glare IV excluded - requires Sacred Sight buff)
    private static readonly (uint level, uint actionId)[] StoneUpgrades = 
    [
        (1, 119),    // Stone
        (18, 127),   // Stone II  
        (54, 3568),  // Stone III
        (72, 16533), // Glare
        (82, 25859)  // Glare III (Glare IV is handled separately as it requires Sacred Sight buff)
    ];
    
    // Aero -> Aero II -> Dia
    private static readonly (uint level, uint actionId)[] AeroUpgrades = 
    [
        (4, 121),    // Aero
        (46, 132),   // Aero II
        (72, 16532)  // Dia
    ];
    
    // Holy -> Holy III
    private static readonly (uint level, uint actionId)[] HolyUpgrades = 
    [
        (45, 139),   // Holy
        (82, 25860)  // Holy III
    ];
    
    // Add more upgrade chains for other jobs...
    
    #endregion
    
    #region Static Lookup Dictionary
    
    // Base action ID -> upgrade chain
    private static readonly Dictionary<uint, (uint level, uint actionId)[]> UpgradeChains = new()
    {
        // WHM Single Target
        [119] = StoneUpgrades,    // Stone
        [127] = StoneUpgrades,    // Stone II
        [3568] = StoneUpgrades,   // Stone III
        [16533] = StoneUpgrades,  // Glare
        [25859] = StoneUpgrades,  // Glare III
        
        // WHM DoT
        [121] = AeroUpgrades,     // Aero
        [132] = AeroUpgrades,     // Aero II
        [16532] = AeroUpgrades,   // Dia
        
        // WHM AoE
        [139] = HolyUpgrades,     // Holy
        [25860] = HolyUpgrades,   // Holy III
        
        // Add more chains for other jobs...
    };
    
    #endregion
    
    #region Elementary Resolution
    
    /// <summary>
    /// Resolves any action to its level-appropriate version.
    /// Ultra-fast static lookup with aggressive inlining.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ResolveToLevel(uint baseActionId, uint playerLevel)
    {
        if (!UpgradeChains.TryGetValue(baseActionId, out var chain))
            return baseActionId; // No upgrade chain - return original
        
        // Find the highest level action the player can use
        uint result = baseActionId;
        foreach (var (level, actionId) in chain)
        {
            if (playerLevel >= level)
                result = actionId;
            else
                break; // Chain is sorted by level - no need to continue
        }
        
        return result;
    }
    
    /// <summary>
    /// Gets the current level-appropriate action using cached game state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ResolveCurrent(uint baseActionId)
    {
        return ResolveToLevel(baseActionId, GameStateCache.Level);
    }
    
    /// <summary>
    /// Check if an action belongs to a specific upgrade chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInChain(uint actionId, uint baseActionId)
    {
        if (!UpgradeChains.TryGetValue(baseActionId, out var chain))
            return false;
            
        foreach (var (_, chainActionId) in chain)
        {
            if (chainActionId == actionId)
                return true;
        }
        
        return false;
    }
    
    #endregion
    
    #region Chain Helpers
    
    /// <summary>Gets all action IDs in the Stone/Glare upgrade chain (excluding Glare IV).</summary>
    public static readonly uint[] StoneGlareChain = [119, 127, 3568, 16533, 25859];
    
    /// <summary>Gets all action IDs in the Aero/Dia upgrade chain.</summary>
    public static readonly uint[] AeroDiaChain = [121, 132, 16532];
    
    /// <summary>Gets all action IDs in the Holy upgrade chain.</summary>
    public static readonly uint[] HolyChain = [139, 25860];
    
    /// <summary>
    /// Gets all possible trigger actions for WHM single target rotation.
    /// These are the actions that should trigger the rotation logic.
    /// Note: Glare IV (37009) is included as a trigger but not in automatic resolution.
    /// </summary>
    public static readonly uint[] WHM_SingleTargetTriggers = [119, 127, 3568, 16533, 25859, 37009];
    
    /// <summary>
    /// Gets all possible trigger actions for WHM AoE rotation.
    /// </summary>
    public static readonly uint[] WHM_AoETriggers = HolyChain;
    
    #endregion
}
