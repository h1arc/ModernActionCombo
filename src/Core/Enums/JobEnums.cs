using System;
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
    // Dense, indexable lookup by JobID (0..42). Using arrays avoids hashing/branching per call.
    private static readonly string[] JobNames =
    [
        // 0..7
        "None", "GLA", "PGL", "MRD", "LNC", "ARC", "CNJ", "THM",
        // 8..15 (crafting)
        "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL",
        // 16..23
        "MIN", "BTN", "FSH", "PLD", "MNK", "WAR", "DRG", "BRD",
        // 24..31
        "WHM", "BLM", "ACN", "SMN", "SCH", "ROG", "NIN", "MCH",
        // 32..39
        "DRK", "AST", "SAM", "RDM", "BLU", "GNB", "DNC", "RPR",
        // 40..42
        "SGE", "VPR", "PCT"
    ];

    // Role lookup table aligned with JobID values.
    private static readonly JobRole[] RolesById =
    [
        // 0..7
        JobRole.None,              // 0 None/Adventurer
        JobRole.Tank,              // 1 GLA
        JobRole.MeleeDPS,          // 2 PGL
        JobRole.Tank,              // 3 MRD
        JobRole.MeleeDPS,          // 4 LNC
        JobRole.PhysicalRangedDPS, // 5 ARC
        JobRole.Healer,            // 6 CNJ
        JobRole.MagicalDPS,        // 7 THM
        // 8..15 (crafting)
        JobRole.CraftingGathering, // 8 CRP
        JobRole.CraftingGathering, // 9 BSM
        JobRole.CraftingGathering, // 10 ARM
        JobRole.CraftingGathering, // 11 GSM
        JobRole.CraftingGathering, // 12 LTW
        JobRole.CraftingGathering, // 13 WVR
        JobRole.CraftingGathering, // 14 ALC
        JobRole.CraftingGathering, // 15 CUL
        // 16..23
        JobRole.CraftingGathering, // 16 MIN
        JobRole.CraftingGathering, // 17 BTN
        JobRole.CraftingGathering, // 18 FSH
        JobRole.Tank,              // 19 PLD
        JobRole.MeleeDPS,          // 20 MNK
        JobRole.Tank,              // 21 WAR
        JobRole.MeleeDPS,          // 22 DRG
        JobRole.PhysicalRangedDPS, // 23 BRD
        // 24..31
        JobRole.Healer,            // 24 WHM
        JobRole.MagicalDPS,        // 25 BLM
        JobRole.Healer,            // 26 ACN (treated as healer per original logic)
        JobRole.MagicalDPS,        // 27 SMN
        JobRole.Healer,            // 28 SCH
        JobRole.MeleeDPS,          // 29 ROG
        JobRole.MeleeDPS,          // 30 NIN
        JobRole.PhysicalRangedDPS, // 31 MCH
        // 32..39
        JobRole.Tank,              // 32 DRK
        JobRole.Healer,            // 33 AST
        JobRole.MeleeDPS,          // 34 SAM
        JobRole.MagicalDPS,        // 35 RDM
        JobRole.Limited,           // 36 BLU
        JobRole.Tank,              // 37 GNB
        JobRole.PhysicalRangedDPS, // 38 DNC
        JobRole.MeleeDPS,          // 39 RPR
        // 40..42
        JobRole.Healer,            // 40 SGE
        JobRole.MeleeDPS,          // 41 VPR
        JobRole.MagicalDPS         // 42 PCT
    ];

    // No static constructor required; arrays above are the single source of truth.

    /// <summary>
    /// Gets the display name for a job ID with O(1) performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetJobName(uint jobId) =>
        jobId < (uint)JobNames.Length ? JobNames[jobId] : $"Unknown({jobId})";

    // Convenience overloads for enum inputs (no behavior change)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetJobName(JobID job) => GetJobName((uint)job);

    // Parsing by short-name removed for simplicity. Add back if needed.

    /// <summary>
    /// Gets the job role for a given job ID using optimized range checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JobRole GetJobRole(uint jobId) =>
        jobId < (uint)RolesById.Length ? RolesById[jobId] : JobRole.None;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JobRole GetJobRole(JobID job) => GetJobRole((uint)job);

    /// <summary>
    /// Checks if a job ID is a healer job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHealer(uint jobId)
        => jobId < (uint)RolesById.Length && RolesById[jobId] == JobRole.Healer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHealer(JobID job) => IsHealer((uint)job);

    /// <summary>
    /// Checks if a job ID is a tank job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]  
    public static bool IsTank(uint jobId)
        => jobId < (uint)RolesById.Length && RolesById[jobId] == JobRole.Tank;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTank(JobID job) => IsTank((uint)job);

    /// <summary>
    /// Checks if a job ID is a DPS job (melee, physical ranged, or magical).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDPS(uint jobId)
        => jobId < (uint)RolesById.Length && (RolesById[jobId] == JobRole.MeleeDPS || RolesById[jobId] == JobRole.PhysicalRangedDPS || RolesById[jobId] == JobRole.MagicalDPS);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDPS(JobID job) => IsDPS((uint)job);

    // Additional helpers like IsCombatJob/IsLimited/IsCraftingGathering/HasRole removed for simplicity.
}
