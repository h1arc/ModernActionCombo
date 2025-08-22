namespace ModernActionCombo.Core.Interfaces;

/// <summary>
/// Interface for job providers that need to handle level changes.
/// Useful for invalidating cached actions when new abilities become available.
/// </summary>
public interface ILevelChangeHandler
{
    /// <summary>
    /// Called when the player's level changes.
    /// Providers can use this to invalidate caches or update ability availability.
    /// </summary>
    /// <param name="newLevel">The new player level</param>
    void OnLevelChanged(uint newLevel);
}

/// <summary>
/// Interface for job providers that need to handle duty state changes.
/// Useful for adapting rotations for different content types.
/// </summary>
public interface IDutyStateHandler
{
    /// <summary>
    /// Called when the player enters or leaves a duty.
    /// Providers can use this to adjust rotation priorities or enable/disable features.
    /// </summary>
    /// <param name="inDuty">True if entering a duty, false if leaving</param>
    /// <param name="dutyId">The ID of the duty being entered (null if leaving)</param>
    void OnDutyStateChanged(bool inDuty, uint? dutyId);
}

/// <summary>
/// Interface for job providers that need to handle combat state changes.
/// Useful for pre-combat setup or post-combat cleanup.
/// </summary>
public interface ICombatStateHandler
{
    /// <summary>
    /// Called when the player enters or leaves combat.
    /// Providers can use this to prepare rotations or reset state.
    /// </summary>
    /// <param name="inCombat">True if entering combat, false if leaving</param>
    void OnCombatStateChanged(bool inCombat);
}
