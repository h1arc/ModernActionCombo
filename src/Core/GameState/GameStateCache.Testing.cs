using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class GameStateCache
{
    #region Testing Support
    
    /// <summary>
    /// Resets the cache to uninitialized state. FOR TESTING ONLY.
    /// </summary>
    public static void ResetForTesting()
    {
        // Reset core state to zero
        if (Vector256.IsHardwareAccelerated)
        {
            Unsafe.As<CoreState, Vector256<uint>>(ref _core) = _zeroVector;
        }
        else
        {
            for (int i = 0; i < 8; i++)
                Lane(i) = 0;
        }
        
        // Reset scalar state
        _gcdRemaining = 0.0f;
        _currentMp = 0;
        _maxMp = 0;
    _lastUpdateTicks = 0;
        _isInitialized = false;
        
        // Clear all tracking dictionaries
    _playerBuffsExpiry.Clear();
    _targetDebuffsExpiry.Clear();
    _actionCooldownsExpiry.Clear();
        
    // Re-initialize tracking data (tests must ensure registry is initialized first)
    InitializeTrackingFromRegistry();
    }
    
    #endregion
}
