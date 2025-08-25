using System.Collections.Generic;

namespace ModernActionCombo.Core.Interfaces;

/// <summary>
/// Provides suggested oGCD action IDs for the current decision window.
/// Implementers should avoid allocations per call; yield from cached buffers if possible.
/// </summary>
public interface IOGCDProvider
{
    /// <summary>
    /// Returns candidate oGCD action IDs to consider, in priority order.
    /// </summary>
    IEnumerable<uint> GetSuggestedOGCDs();
}