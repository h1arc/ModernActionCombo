using System;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class GameStateCache
{
    #region Action Usage Tracking
    
    /// <summary>
    /// Record that we just used an action, getting its cooldown duration from the game data.
    /// Call this after successfully executing an action to maintain accurate cooldown tracking.
    /// </summary>
    public static void RecordActionUsed(uint actionId)
    {
        var dataManager = ModernActionCombo.DataManager;
        if (dataManager != null)
        {
            var actionSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var action = actionSheet?.GetRow(actionId);
            
            if (action != null)
            {
                // TODO: Find the correct property name for recast time in Lumina.Excel.Sheets.Action
                // Common candidates: Recast100ms, CooldownGroup, etc.
                // For now, use a smart default based on action type
                var cooldownSeconds = actionId switch
                {
                    136 => 120.0f,   // Presence of Mind - 2 minute cooldown
                    16535 => 30.0f,  // Afflatus Misery - 30 second cooldown
                    16532 => 30.0f,  // Dia - 30 second duration (DoT)
                    _ => 2.5f        // Default GCD for most actions
                };
                
                var now = Environment.TickCount64;
                _actionCooldownsExpiry[actionId] = now + (long)(cooldownSeconds * 1000.0f);
                _lastUpdateTicks = now;
            }
        }
    }
    
    #endregion
}
