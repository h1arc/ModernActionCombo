using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Enums;

/// <summary>
/// Job role classifications for organizing combo logic by job type.
/// </summary>
public enum JobRole : byte
{
    None = 0,
    Tank = 1,
    Healer = 2,
    MeleeDPS = 3,
    PhysicalRangedDPS = 4,
    MagicalDPS = 5,
    CraftingGathering = 6,
    Limited = 7
}

/// <summary>
/// Job IDs corresponding to FFXIV job system.
/// Used for job-specific combo logic and validation.
/// </summary>
public enum JobID : uint
{
    #region Base Classes
    None = 0,
    Adventurer = 0,
    #endregion

    #region Tanks
    Gladiator = 1,
    PLD = 19,      // Paladin
    Marauder = 3,
    WAR = 21,      // Warrior
    DRK = 32,      // Dark Knight
    GNB = 37,      // Gunbreaker
    #endregion

    #region Healers
    Conjurer = 6,
    WHM = 24,      // White Mage
    Arcanist = 26,
    SCH = 28,      // Scholar
    AST = 33,      // Astrologian
    SGE = 40,      // Sage
    #endregion

    #region Melee DPS
    Pugilist = 2,
    MNK = 20,      // Monk
    Lancer = 4,
    DRG = 22,      // Dragoon
    Rogue = 29,
    NIN = 30,      // Ninja
    SAM = 34,      // Samurai
    RPR = 39,      // Reaper
    VPR = 41,      // Viper
    #endregion

    #region Physical Ranged DPS
    Archer = 5,
    BRD = 23,      // Bard
    MCH = 31,      // Machinist
    DNC = 38,      // Dancer
    #endregion

    #region Magical DPS
    Thaumaturge = 7,
    BLM = 25,      // Black Mage
    SMN = 27,      // Summoner (shares base with Arcanist)
    RDM = 35,      // Red Mage
    BLU = 36,      // Blue Mage (Limited)
    PCT = 42,      // Pictomancer
    #endregion

    #region Crafting & Gathering
    CRP = 8,       // Carpenter
    BSM = 9,       // Blacksmith
    ARM = 10,      // Armorer
    GSM = 11,      // Goldsmith
    LTW = 12,      // Leatherworker
    WVR = 13,      // Weaver
    ALC = 14,      // Alchemist
    CUL = 15,      // Culinarian
    MIN = 16,      // Miner
    BTN = 17,      // Botanist
    FSH = 18,      // Fisher
    #endregion
}

/// <summary>
/// Helper class for job-related utilities.
/// Optimized for high-performance lookups and minimal allocations.
/// </summary>
public static class JobHelper
{
    // Pre-computed job name mappings for O(1) lookup performance
    private static readonly Dictionary<uint, string> JobNames = new()
    {
        [0] = "None",
        [1] = "GLA", [19] = "PLD",
        [2] = "PGL", [20] = "MNK", 
        [3] = "MRD", [21] = "WAR",
        [4] = "LNC", [22] = "DRG",
        [5] = "ARC", [23] = "BRD",
        [6] = "CNJ", [24] = "WHM",
        [7] = "THM", [25] = "BLM",
        [8] = "CRP", [9] = "BSM", [10] = "ARM", [11] = "GSM",
        [12] = "LTW", [13] = "WVR", [14] = "ALC", [15] = "CUL",
        [16] = "MIN", [17] = "BTN", [18] = "FSH",
        [26] = "ACN", [27] = "SMN", [28] = "SCH",
        [29] = "ROG", [30] = "NIN",
        [31] = "MCH", [32] = "DRK", [33] = "AST",
        [34] = "SAM", [35] = "RDM", [36] = "BLU",
        [37] = "GNB", [38] = "DNC", [39] = "RPR",
        [40] = "SGE", [41] = "VPR", [42] = "PCT"
    };

    /// <summary>
    /// Gets the display name for a job ID with O(1) performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetJobName(uint jobId) => 
        JobNames.TryGetValue(jobId, out var name) ? name : $"Unknown({jobId})";

    /// <summary>
    /// Gets the job role for a given job ID using optimized range checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JobRole GetJobRole(uint jobId) => jobId switch
    {
        // Tanks (optimized with range patterns)
        1 or 19 or 3 or 21 or 32 or 37 => JobRole.Tank,
        
        // Healers  
        6 or 24 or 26 or 28 or 33 or 40 => JobRole.Healer,
        
        // Melee DPS
        2 or 20 or 4 or 22 or 29 or 30 or 34 or 39 or 41 => JobRole.MeleeDPS,
        
        // Physical Ranged DPS
        5 or 23 or 31 or 38 => JobRole.PhysicalRangedDPS,
        
        // Magical DPS
        7 or 25 or 27 or 35 or 42 => JobRole.MagicalDPS,
        
        // Limited Jobs
        36 => JobRole.Limited,
        
        // Crafting & Gathering (range optimization)
        >= 8 and <= 18 when jobId != 19 => JobRole.CraftingGathering,
        
        _ => JobRole.None
    };

    /// <summary>
    /// Checks if a job ID is a valid combat job with inlined role check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCombatJob(uint jobId) => jobId switch
    {
        // Direct combat job checks (eliminates GetJobRole call)
        1 or 19 or 3 or 21 or 32 or 37 or // Tanks
        6 or 24 or 26 or 28 or 33 or 40 or // Healers  
        2 or 20 or 4 or 22 or 29 or 30 or 34 or 39 or 41 or // Melee DPS
        5 or 23 or 31 or 38 or // Physical Ranged DPS
        7 or 25 or 27 or 35 or 42 // Magical DPS
        => true,
        _ => false
    };

    /// <summary>
    /// Checks if a job ID is a healer job with direct comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHealer(uint jobId) => jobId is 6 or 24 or 26 or 28 or 33 or 40;

    /// <summary>
    /// Checks if a job ID is a tank job with direct comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]  
    public static bool IsTank(uint jobId) => jobId is 1 or 19 or 3 or 21 or 32 or 37;

    /// <summary>
    /// Checks if a job ID is a DPS job with direct comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDPS(uint jobId) => jobId switch
    {
        2 or 20 or 4 or 22 or 29 or 30 or 34 or 39 or 41 or // Melee DPS
        5 or 23 or 31 or 38 or // Physical Ranged DPS  
        7 or 25 or 27 or 35 or 42 // Magical DPS
        => true,
        _ => false
    };
}
