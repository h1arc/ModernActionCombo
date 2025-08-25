using System;

namespace ModernActionCombo;

// Minimal movement detection kept as a separate partial for clarity
public sealed partial class ModernActionCombo
{
    private bool DetectMovement()
    {
        var player = ClientState.LocalPlayer;
        if (player == null) return false;

        try
        {
            unsafe
            {
                var agentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
                if (agentMap != null)
                {
                    return agentMap->IsPlayerMoving;
                }
            }
        }
        catch
        {
            // Ignore and fallback
        }

        // Fallback: simple position delta with small threshold
        var now = Environment.TickCount64;
        var pos = player.Position;
        if (_lastPositionUpdate == 0)
        {
            _lastPosition = pos;
            _lastPositionUpdate = now;
            return false;
        }

        var distSq = (pos - _lastPosition).LengthSquared();
        if (now - _lastPositionUpdate > 100)
        {
            _lastPosition = pos;
            _lastPositionUpdate = now;
        }
        const float thresholdSq = MOVEMENT_THRESHOLD * MOVEMENT_THRESHOLD;
        return distSq > thresholdSq;
    }
}
